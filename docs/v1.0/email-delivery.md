# Account email delivery

The API selects its email transport from the host environment. Development uses local SMTP through smtp4dev. Other environments use Azure Communication Services Email. The application layer depends only on `IConfirmationEmailSender`; both transports live in Infrastructure.

## Local development with smtp4dev

1. Start `rnwood.smtp4dev` and confirm its SMTP listener is running in the smtp4dev UI.
2. The checked-in API development settings use `127.0.0.1:25`, no TLS, and no SMTP authentication. Change `Email:Smtp4Dev:Port` in `src/PersonalFinance.Api/appsettings.Development.json` to the SMTP port shown by your smtp4dev instance if it differs.
3. Run the API and Blazor client in Development. Registration and resend messages appear in the smtp4dev web UI; the email link opens the client at `http://localhost:5136/confirm-email`.

The sender address uses the non-deliverable `.test` domain. The API does not log SMTP conversations, message bodies, recipients, or confirmation links.

## Production with Azure Communication Services Email

The API uses `DefaultAzureCredential`; do not add Azure keys or connection strings to the repository or Blazor client. Provision and connect an Azure Communication Services resource with an Email Communication resource and the selected Azure-managed sender domain. Enable a managed identity for the API host and grant it the required email-send access on the Communication Services resource.

Configure these values in the production API host's backend settings:

| Setting | Value |
| --- | --- |
| `Email:ClientBaseUrl` | Absolute public URL of the Blazor client |
| `Email:SenderAddress` | Verified MailFrom address for the connected Azure-managed domain |
| `Email:SenderName` | Display name, such as `Personal Finance` |
| `Email:Azure:Endpoint` | HTTPS endpoint of the Communication Services resource |
| `ConnectionStrings:Identity` | Production Identity database connection |

The account endpoints are not mapped unless both Identity database and environment-appropriate email configuration are present. Configure the Azure resource, managed identity permissions, sender address and endpoint before enabling Production registration.

Azure send-operation completion means the message was accepted for outbound delivery; it does not confirm delivery to the recipient's inbox. Delivery-event processing, cost/quota review, and a controlled live-send check remain release work.

## Tests

Automated tests use fake email transports and synthetic addresses. They do not connect to smtp4dev or Azure and never send to real recipients.
