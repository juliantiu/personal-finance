# UC-005 card protection and local operation

Phase 2 configuration and recovery notes, 12 November 2026. Tests use synthetic numbers, isolated databases and test-owned keys. No ordinary development migration or deployment is performed by this phase.

## Backend configuration

Persistent card saving requires both:

- The existing UC-020 encrypted persistent Data Protection ring: `Authentication:KeyPersistence:Enabled=true`, a stable absolute `KeysPath`, stable `ApplicationName`, and the configured Data Protection certificate/private key. Keep the signing/encryption certificate configuration in [UC-020 deployment notes](../UC-020_sign-in-and-sign-out/deployment-and-verification.md).
- `Cards:MatchingKey`: Base64 encoding of a dedicated cryptographically random 32-byte key, supplied only through protected backend configuration (`Cards__MatchingKey` as an environment variable). Generate it once for the database; retain it across restarts/instances and back it up securely. Never reuse authentication keys, commit its value, expose it to the client, or generate a replacement on startup.

No live secret or developer setting is installed. Missing, malformed or wrong-length matching configuration, or a composition root without persistent encrypted keys, produces a safe card-save 503. Registration, profile creation and masked reads remain available. Disposable test hosts explicitly substitute synthetic readiness through test composition; there is no production configuration switch for this exception.

Encryption purpose chain: `PersonalFinance.CardNumber`, `v1`, profile GUID in `N` format, method GUID in `N` format. No number appears in a purpose. Copied ciphertext cannot decrypt for another profile/method. Ordinary reads project only the saved suffix and never decrypt.

Matching uses HMAC-SHA-256 over ASCII `profile-guid-N:canonical-number`. The nonsecret key identifier is uppercase hexadecimal SHA-256 of the random matching key. A transaction checks every stored card's identifier before accepting a write. The unique index excludes the key identifier so different key generations cannot silently bypass uniqueness.

## Migration and atomic writes

`20261112103729_AddEncryptedCards` extends `PaymentMethods`, preserving Cash rows/IDs and its filtered uniqueness index. It adds encrypted details, a 32-byte fingerprint, key identifier, last-four suffix, nullable `datetimeoffset` retirement metadata and SQL rowversion. Shape checks distinguish Cash/cards; unique `(ProfileId, NumberFingerprint)` covers every card, including retained cards.

The adapter uses a transaction-owned exclusive SQL application lock `PersonalFinance.CardWrites` with a 10-second lock timeout, then serializable access/key/duplicate checks and one atomic save. This deliberately serializes the small card-write workload across instances. SQL also rejects duplicates from direct provider writes. Failures never trigger automatic submission; an unconfirmed result requires refresh.

The migration is verified from the prior profile-creation baseline in isolated LocalDB. Applying it elsewhere requires a separately agreed migration step with compatible backups/configuration. Downgrade refuses to remove protected columns while cards exist; prefer a compatible application rollback.

## Backup, restore and protection-key retention

Back up the database, entire protection ring, required certificate private keys/password access, stable application name and matching key as one recoverable set. A database-only backup is insufficient. Keep backup material outside source control/client assets. Retain old protection keys and decryption certificates while stored cards depend on them; normal key rotation must retain decryption material.

Restore into isolation with public writes disabled. Restore compatible key configuration first, satisfy the project's erasure/access recovery requirements, verify masked reads and a controlled synthetic protection/matching round trip before enabling access. A local host-restart check demonstrates key continuity, not deployed disaster recovery or permission to provision resources.

## Controlled matching-key replacement

Phase 2 has no automatic rotation/reindex command. Prepare and review a maintenance operation before replacement:

1. Stop card writes on all instances and take a recoverable backup set. Preserve existing protection keys; losing a matching key does not itself erase/decrypt stored numbers.
2. Rehearse in isolation: decrypt each retained card using its profile/method purpose and compute replacement fingerprints/identifiers with the new random key. Never log/export numbers or write temporary plaintext files.
3. Check canonical duplicates across active and retired methods per profile. Resolve inconsistencies explicitly; never silently merge identities or discard references.
4. Hold the same write application lock and replace all fingerprints/identifiers in one reviewed transaction with uniqueness constraints active. Retain ciphertext, IDs and references. Never mix generations or disable the index to bypass conflicts.
5. Configure every instance with the new matching key before reopening writes. Verify compatibility, same-profile duplicates and independent-profile saving with synthetic data. On failure keep writes closed and restore the database/configuration pair together.

Simply changing `Cards:MatchingKey` must fail readiness. Building or executing a real maintenance command requires separately approved scope.

## Nickname migration amendment

`20261112164221_AddPaymentMethodNickname` adds nullable `Nickname` (`nvarchar(100)`), backfills existing Cash methods with `Cash` and leaves existing cards null. New Cash defaults are supplied at profile creation. This migration preserves card IDs, ciphertext, fingerprints and card versions; nickname input never enters encryption purposes or matching fingerprints. It is tested only on isolated databases. Renaming existing methods, including Cash, waits for the editing phase.
