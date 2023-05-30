using billpg.BearerTokenExample;

/* Announce start. */
Console.WriteLine("TokenExchangeHelper demo.");

/* Construct the helper, allowing both initiator and issuer to select keys. */
var helper = new TokenExchangeHelper();
Console.WriteLine("InitiatorKeyText: " + helper.InitiatorKeyText);

/* Hashes used in verify step. */
Console.WriteLine("VerifyEvidenceForInitiator: " + helper.VerifyEvidenceForInitiator);
Console.WriteLine("VerifyEvidenceForIssuer: " + helper.VerifyEvidenceForIssuer);

/* Generate bearer token. */
string bearerToken = "This_is_an_impossible_to_guess_Bearer_token_for_alice.example";
Console.WriteLine($"BearerToken: {bearerToken}");

/* Sign the bearer token. */
var bearerTokenSignature = helper.SignBearerToken(bearerToken);
Console.WriteLine($"TokenSignature: {bearerTokenSignature}");

