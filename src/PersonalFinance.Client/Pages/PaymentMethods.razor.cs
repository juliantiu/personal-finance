using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Components.WebAssembly.Http;
using PersonalFinance.Contracts.Profiles;

namespace PersonalFinance.Client.Pages;

public partial class PaymentMethods
{
    private string _cardNumber = "";
    private string _nickname = "";
    private string? _nicknameError;
    private string? _numberError;
    private string? _cardError;
    private bool _saving;
    private bool _uncertain;
    private bool _refreshedAfterSave;
    private bool _cardSuccess;
    private int _saveGeneration;
    private CancellationTokenSource? _cardCancellation;
    private static readonly Dictionary<string, object> CardInputAttributes = new()
    { ["autocomplete"] = "off", ["inputmode"] = "numeric", ["spellcheck"] = "false" };

    private void ResetCardDraft()
    {
        ++_saveGeneration;
        _cardCancellation?.Cancel();
        _cardNumber = "";
        _nickname = "";
        _nicknameError = null;
        _numberError = _cardError = null;
        _saving = _uncertain = _refreshedAfterSave = _cardSuccess = false;
    }

    private void CardNumberChanged(string value)
    {
        _cardNumber = value;
        _numberError = null;
        _cardSuccess = false;
    }

    private void NicknameChanged(string value)
    {
        _nickname = value;
        _nicknameError = null;
        _cardSuccess = false;
    }

    private async Task SaveCardAsync()
    {
        if (_saving || _uncertain || _methods?.Role is not ("owner" or "editor")) return;
        _cardError = _numberError = _nicknameError = null;
        _cardSuccess = false;
        if (string.IsNullOrWhiteSpace(_cardNumber)) { _numberError = "Enter a card number."; return; }
        if (_nickname.Trim().Length > 100) { _nicknameError = "Use at most 100 characters after trimming."; return; }
        var generation = ++_saveGeneration;
        var profileId = ProfileId;
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
        _cardCancellation = cancellation;
        var token = cancellation.Token;
        _saving = true;
        var postStarted = false;
        try
        {
            using var proofRequest = CardRequest(HttpMethod.Get, "api/auth/antiforgery");
            using var proofResponse = await Http.SendAsync(proofRequest, token);
            if (generation != _saveGeneration) return;
            if (proofResponse.StatusCode == HttpStatusCode.Unauthorized) { CardSessionLost(profileId); return; }
            proofResponse.EnsureSuccessStatusCode();
            var proof = await proofResponse.Content.ReadFromJsonAsync<CardProof>(cancellationToken: token);
            if (string.IsNullOrWhiteSpace(proof?.RequestToken)) throw new JsonException("Missing request proof.");
            if (generation != _saveGeneration) return;
            using var request = CardRequest(HttpMethod.Post, $"api/budget-profiles/{profileId}/payment-methods");
            request.Headers.Add("X-CSRF-TOKEN", proof.RequestToken);
            request.Content = JsonContent.Create(new SaveCardRequest { Number = _cardNumber, Nickname = _nickname });
            postStarted = true;
            using var response = await Http.SendAsync(request, token);
            if (generation != _saveGeneration) return;
            if (response.StatusCode == HttpStatusCode.Unauthorized) { CardSessionLost(profileId); return; }
            if (response.StatusCode == HttpStatusCode.NotFound)
            { ResetCardDraft(); _methods = null; _unavailable = true; return; }
            if (response.StatusCode == HttpStatusCode.Forbidden)
            { ResetCardDraft(); if (_methods is not null) _methods = _methods with { Role = "view_only" }; return; }
            if (response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Conflict)
            {
                _cardError = "Your card wasn't saved. Check the form and try again.";
                var feedback = await response.Content.ReadFromJsonAsync<CardFeedback>(cancellationToken: token);
                if (generation != _saveGeneration) return;
                var errors = feedback?.Errors?.FirstOrDefault(field => string.Equals(field.Key, "Number", StringComparison.OrdinalIgnoreCase)).Value;
                if (errors is not null) _numberError = string.Join(" ", errors);
                var nicknameErrors = feedback?.Errors?.FirstOrDefault(field => string.Equals(field.Key, "Nickname", StringComparison.OrdinalIgnoreCase)).Value;
                if (nicknameErrors is not null) _nicknameError = string.Join(" ", nicknameErrors);
                return;
            }
            if (response.StatusCode != HttpStatusCode.Created) { _uncertain = true; return; }
            var saved = await response.Content.ReadFromJsonAsync<ProfilePaymentMethodDetailsResponse>(cancellationToken: token);
            if (generation != _saveGeneration) return;
            if (saved is null || saved.Id == Guid.Empty || saved.Kind != "credit_card" || string.IsNullOrWhiteSpace(saved.MaskedIdentifier))
                throw new JsonException("Missing saved method identity.");
            _cardNumber = "";
            _nickname = "";
            _cardSuccess = true;
            await LoadAsync();
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
        catch (Exception error) when (error is HttpRequestException or OperationCanceledException or JsonException)
        {
            if (generation != _saveGeneration) return;
            if (postStarted) _uncertain = true;
            else _cardError = "We couldn't prepare your request. Your card wasn't submitted. Please try again.";
        }
        finally
        {
            if (generation == _saveGeneration) _saving = false;
            if (ReferenceEquals(_cardCancellation, cancellation)) _cardCancellation = null;
        }
    }

    private Task RefreshCardsAsync()
    {
        if (_saving || _loading) return Task.CompletedTask;
        _refreshedAfterSave = false;
        return LoadAsync();
    }

    private Task RetryCardAsync()
    {
        if (!_refreshedAfterSave || _saving) return Task.CompletedTask;
        _uncertain = _refreshedAfterSave = false;
        return SaveCardAsync();
    }

    private void CardSessionLost(Guid profileId)
    {
        ResetCardDraft();
        Authentication.NotifySignedOut();
        Navigation.NavigateTo($"/login?returnUrl={Uri.EscapeDataString($"/budget-profiles/{profileId}/payment-methods")}", replace: true);
    }

    private static HttpRequestMessage CardRequest(HttpMethod method, string path)
    {
        var request = new HttpRequestMessage(method, path);
        request.SetBrowserRequestCredentials(BrowserRequestCredentials.Include);
        return request;
    }

    private sealed record CardProof(string? RequestToken);
    private sealed record CardFeedback(Dictionary<string, string[]>? Errors);
}
