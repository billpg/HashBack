using billpg.BearerTokenExample;

/* Announce start. */
Console.WriteLine("TokenExchangeHelper demo.");

/* Generate the pre-flight dedication string. */
Console.WriteLine("Preflight Dedicatiom Text: " + PreFlight.DedicationText);

/* Construct the helper, allowing both initiator and issuer to select keys. */
var helper = new TokenExchangeHelper();
Console.WriteLine("InitiatorKeyText: " + helper.InitiatorKeyText);

/* Hashes used in verify step. */
Console.WriteLine("VerifyEvidenceForInitiator: " + helper.VerifyEvidenceForInitiator);
Console.WriteLine("VerifyEvidenceForIssuer: " + helper.VerifyEvidenceForIssuer);

/* Generate bearer token. */
var initiatorDomainName = "initiator.example";
Console.WriteLine("Initiator Domain: " + initiatorDomainName);
string bearerToken = "This_is_an_impossible_to_guess_Bearer_token_for_" + initiatorDomainName;
Console.WriteLine($"BearerToken: {bearerToken}");

/* Sign the bearer token. */
var bearerTokenSignature = helper.SignBearerToken(bearerToken);
Console.WriteLine($"TokenSignature: {bearerTokenSignature}");

