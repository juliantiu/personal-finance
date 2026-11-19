using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using PersonalFinance.Contracts.Profiles;

namespace PersonalFinance.Client.Pages;

public partial class PaymentMethods
{
    private ProfilePaymentMethodDetailsResponse? _revealMethod;
    private string? _revealedNumber;
    private string? _revealError;
    private bool _revealing;
    private int _revealGeneration;
    private CancellationTokenSource? _revealCancellation;

    private Task OpenRevealAsync(ProfilePaymentMethodDetailsResponse method)
    {
        CloseReveal();
        if (method.Kind != "credit_card" || _methods?.Role is not ("owner" or "editor")) return Task.CompletedTask;
        _revealMethod = method;
        return FetchRevealAsync();
    }

    private void HideReveal()
    {
        ++_revealGeneration;
        _revealCancellation?.Cancel();
        _revealedNumber = _revealError = null;
        _revealing = false;
    }

    private void CloseReveal()
    {
        HideReveal();
        _revealMethod = null;
    }

    private void RevealVisibilityChanged(bool visible)
    {
        if (!visible) CloseReveal();
    }

    private Task ToggleRevealAsync()
    {
        if (_revealedNumber is null && !_revealing) return FetchRevealAsync();
        HideReveal();
        return Task.CompletedTask;
    }

    private async Task FetchRevealAsync()
    {
        if (_revealing || _revealMethod is null || _methods?.Role is not ("owner" or "editor")) return;
        HideReveal();
        var generation = _revealGeneration;
        var profile = ProfileId;
        var method = _revealMethod.Id;
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
        _revealCancellation = cancellation;
        var token = cancellation.Token;
        _revealing = true;
        try
        {
            using var proofRequest = CardRequest(HttpMethod.Get, "api/auth/antiforgery");
            using var proofResponse = await Http.SendAsync(proofRequest, token);
            if (generation != _revealGeneration) return;
            if (proofResponse.StatusCode == HttpStatusCode.Unauthorized) { CardSessionLost(profile); return; }
            proofResponse.EnsureSuccessStatusCode();
            var proof = await proofResponse.Content.ReadFromJsonAsync<CardProof>(cancellationToken: token);
            if (generation != _revealGeneration) return;
            if (string.IsNullOrWhiteSpace(proof?.RequestToken)) throw new JsonException("Missing request proof.");
            using var request = CardRequest(HttpMethod.Post, $"api/budget-profiles/{profile}/payment-methods/{method}/reveal");
            request.Headers.Add("X-CSRF-TOKEN", proof.RequestToken);
            using var response = await Http.SendAsync(request, token);
            if (generation != _revealGeneration) return;
            if (response.StatusCode == HttpStatusCode.Unauthorized) { CardSessionLost(profile); return; }
            if (response.StatusCode == HttpStatusCode.Forbidden)
            {
                ResetCardDraft();
                if (_methods is not null) _methods = _methods with { Role = "view_only" };
                return;
            }
            response.EnsureSuccessStatusCode();
            var revealed = await response.Content.ReadFromJsonAsync<RevealedCardResponse>(cancellationToken: token);
            if (generation != _revealGeneration) return;
            if (revealed?.Number is not { Length: >= 12 and <= 19 } number || number.Any(c => c is < '0' or > '9'))
                throw new JsonException("Invalid reveal response.");
            _revealedNumber = number;
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
        catch (Exception error) when (error is HttpRequestException or OperationCanceledException or JsonException)
        {
            if (generation == _revealGeneration)
            {
                _revealedNumber = null;
                _revealError = "We couldn't reveal this card. It may be unavailable or your access may have changed. Close and refresh payment methods, or try again.";
            }
        }
        finally
        {
            if (generation == _revealGeneration) _revealing = false;
            if (ReferenceEquals(_revealCancellation, cancellation)) _revealCancellation = null;
        }
    }
}
