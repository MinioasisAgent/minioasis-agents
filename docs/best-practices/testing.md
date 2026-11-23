- **Access:** Level 2
- **Last Updated:** 2026-11-23

# Testing

Rules `TS-001` to `TS-013`. The format, and the meaning of the made-up "renew a loan" examples, are explained in [`README.md`](README.md).

A test exists to fail when the behavior it covers breaks. Every rule here serves that purpose.

## TS-001 Write the test first and watch it fail

A test that has never failed proves nothing: it may pass whether or not the behavior exists. Write the test for a requirement before the code that satisfies it, and see it fail for the expected reason. When you add a test for behavior that already exists, break that behavior on purpose, see the test fail, and restore the code.

```text
BAD    Write the code, then a test, see it pass, and move on.
GOOD   Write the test. Run it: it fails because the behavior is missing. Write the code. Run it: it passes.
```

**Checked by:** review, and independent verification (`docs/process.md`, section 8)

---

## TS-002 Test a behavior through the component that owns it

Test each behavior on the component that owns it, in that component's test file. A feature's tests cover what the feature itself decides: its validation, its mapping, its translation of failures. A shared component, such as the handler that enforces the deadline or the provider that caches tokens, is tested directly.

When a shared behavior is proven only through one feature's tests, it looks like part of that feature, it is retested by accident for every new feature, and it loses its proof when that feature changes.

```text
BAD    Minioasis.Koha.Tests/Loans/Adapters/KohaRenewLoanAdapterTests.cs
       … contains the tests for the shared deadline and for token refresh

GOOD   Minioasis.Koha.Tests/Shared/Handlers/KohaDeadlineHandlerTests.cs              (the deadline)
       Minioasis.Koha.Tests/Authentication/Providers/KohaAccessTokenProviderTests.cs (tokens)
       Minioasis.Koha.Tests/Loans/Adapters/KohaRenewLoanAdapterTests.cs              (only the adapter's own translation)
```

---

## TS-003 Keep provider knowledge inside the integration project's tests

A test project knows only what its production project knows. Koha paths, payloads, and token formats appear only in `Minioasis.Koha.Tests`. Tests of Application and Api replace an Application-owned interface, such as a use case or a gateway, and never fake what lies beneath it.

Otherwise a change in Koha's protocol breaks tests in a project that has nothing to do with Koha, and the boundary in `AR-007` exists in production code only.

```csharp
// BAD: in Minioasis.Api.Tests, a fake network that knows Koha's token endpoint and JSON
if (request.RequestUri!.AbsolutePath == "/api/v1/oauth/token")
{
    return Json("""{"access_token":"t","token_type":"Bearer","expires_in":3600}""");
}

// GOOD: in Minioasis.Api.Tests, replace the Application-owned gateway
services.RemoveAll<IRenewLoanGateway>();
services.AddSingleton<IRenewLoanGateway>(new FakeRenewLoanGateway(RenewLoanGatewayResult.Renewed(response)));
```

---

## TS-004 Use the right test double for each project

Each project is tested at its own boundary, with the lightest double that reaches it:

| Test project | Exercises | Through |
| --- | --- | --- |
| `Minioasis.Application.Tests` | Business rules and use-case outcomes | Fakes of Application's interfaces |
| `Minioasis.Api.Tests` | HTTP contracts and presentation | An in-process host with service overrides |
| `Minioasis.Koha.Tests` | Provider translation, authentication, cancellation, and failure handling | `HttpMessageHandler` doubles |

Do not build or extend a fake network server for routine tests. A server that listens on a port is slow, needs a free port, and tests the network stack instead of the application.

```csharp
// BAD: a real listener for a unit test
using var server = new HttpListener();
server.Prefixes.Add("http://localhost:5099/");

// GOOD: a handler double, in Minioasis.Koha.Tests
var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
```

---

## TS-005 Prove the wiring separately from the behavior

Tests that replace a component with a double cannot notice that the real component is missing or not connected. So the wiring needs its own proof:

1. **Each project that registers services** has a test that resolves them from its own registration method, and checks their order where order matters (for example, the order of HTTP handlers).
2. **For each capability,** one Api test starts the host with only the gateway replaced, and shows that the value in the route reaches the gateway through the real use case.
3. **For each capability,** one Api test resolves the gateway from the unmodified host and shows that it resolves.

```csharp
// GOOD: only the gateway is replaced; the endpoint, the mapper, and the use case are real
[Fact]
public async Task Route_value_reaches_the_gateway_through_the_real_use_case()
{
    var gateway = new FakeRenewLoanGateway(RenewLoanGatewayResult.Failed(RenewLoanOutcome.NotFound));
    await using var factory = new ApiTestHostFactory(services =>
    {
        services.RemoveAll<IRenewLoanGateway>();
        services.AddSingleton<IRenewLoanGateway>(gateway);
    });

    using var response = await factory.CreateClient().PostAsync(
        "/api/v1/loans/77/renewal",
        content: null,
        TestContext.Current.CancellationToken);

    Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    Assert.Equal(77, gateway.LastLoanId!.Value);
}
```

```text
BAD    Every Api test replaces the use case. The endpoint could ignore the route value, or the
       use case could be missing from dependency injection, and every test would still pass.
```

**Checked by:** review, and independent verification ("wired")

---

## TS-006 Cover what the requirement's words cover

Test what the requirement says, not one convenient case of it. Where a requirement names several classes of input, test each one. Where it uses "any", "every", "all", or "never", the class is open: test at least three different members of it, including its edges.

One example proves one example. A change that breaks every other member of the class would pass.

```csharp
// Requirement: "any 5xx answer from Koha is reported as unavailable"

// BAD: one member of the class
[InlineData(HttpStatusCode.BadGateway)]

// GOOD: both edges and the middle
[InlineData(HttpStatusCode.InternalServerError)]   // 500, the lowest
[InlineData(HttpStatusCode.BadGateway)]            // 502
[InlineData(HttpStatusCode.ServiceUnavailable)]    // 503
[InlineData((HttpStatusCode)599)]                  // the highest
```

The same applies to words such as "not a whole number" (test letters, a decimal, a thousands separator, an exponent) and "not a GET" (test several other methods).

**Checked by:** review, and independent verification

---

## TS-007 Make a broken behavior fail the test, not hang it

A test that waits without a limit turns a broken behavior into a run that never ends. Nobody learns which test is stuck, and a CI job is wasted until it times out. Give every wait in a test a limit, so that the test fails and names itself.

```csharp
// BAD: waits for ever if the cancellation is never observed
var result = await pending;

// GOOD: fails after five seconds, with this test's name
var result = await pending.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
```

Prefer a controllable clock (`TimeProvider`) to real delays. Where a test must measure real time, give the assertion a stated margin that is wide enough for a busy machine and narrow enough to catch the fault it is looking for.

---

## TS-008 Keep tests free of live systems

Tests need no network, no live Koha server, and no developer credentials. They run the same way on any machine and in CI, and they can never change a real record. The only exception is the separate set of test Koha checks that `TS-013` describes.

```csharp
// BAD: depends on a server and on real credentials
var options = new KohaOptions
{
    BaseUrl = new Uri("https://koha.example.org/api/v1/"),
    ClientId = Environment.GetEnvironmentVariable("KOHA_ID")!,
    ClientSecret = Environment.GetEnvironmentVariable("KOHA_SECRET")!
};

// GOOD: dummy configuration and a handler double
var options = new KohaOptions
{
    BaseUrl = new Uri("https://koha.test/api/v1/"),
    ClientId = "id",
    ClientSecret = "secret"
};
```

**Checked by:** review; CI runs without credentials

---

## TS-009 Use one shared API test host, and keep its configuration isolated

Api tests start the application through one shared test host factory, kept under `Minioasis.Api.Tests/Shared/`. It supplies dummy Koha configuration, and it accepts service overrides and settings for one host. Tests do not build their own hosts in other ways.

Tests run in parallel, so configuration must belong to one host. Do not change process-wide environment variables in a test, because another test running at the same moment would see the change.

```csharp
// BAD: leaks into every other test in the process
Environment.SetEnvironmentVariable("MINIOASIS_KOHA_BASE_URL", "https://other.test/api/v1/");

// GOOD: settings for this host only
await using var factory = new ApiTestHostFactory(
    settings: new Dictionary<string, string?> { ["MINIOASIS_KOHA_BASE_URL"] = "https://other.test/api/v1/" });
```

---

## TS-010 Assert outcomes, not log wording

Assert what happened: the result, the status, the calls that were made, the level of what was logged. Do not prove a behavior by matching the wording of a log message. Log wording is free to change, so such a test breaks for no reason, or, worse, keeps passing when a differently worded message appears.

```csharp
// Requirement: "an abandoned request is not logged as a failure"

// BAD: passes for any error whose message is worded differently
Assert.DoesNotContain(logs.Entries, entry => entry.Message.Contains("Unhandled API failure"));

// GOOD: no entry at Error level or above, whatever its wording
Assert.DoesNotContain(logs.Entries, entry => entry.Level >= LogLevel.Error);
```

Text that a brief fixes is different. A client-visible error title or detail is part of the public contract and is asserted exactly.

---

## TS-011 Mirror production folders, and put reusable doubles under `TestDoubles`

A test project has the same feature and role folders as the project it tests, so the tests for a type are found where the type is. A double or harness that more than one test class uses is a top-level type in its own file: under `<Feature>/TestDoubles` when one feature uses it, and under `Shared/TestDoubles` when several do.

```text
Minioasis.Koha/Loans/Adapters/KohaRenewLoanAdapter.cs
Minioasis.Koha.Tests/Loans/Adapters/KohaRenewLoanAdapterTests.cs
Minioasis.Koha.Tests/Loans/TestDoubles/FakeRenewalClock.cs
Minioasis.Koha.Tests/Shared/TestDoubles/StubHttpMessageHandler.cs
```

```text
BAD    A two-hundred-line harness nested inside one test class, which a second test class then copies
```

A small helper that one test class uses may stay nested in that class.

---

## TS-012 Add tests for every changed behavior and its meaningful failure paths

A change in behavior comes with focused tests for the new behavior and for the ways it can fail that matter: invalid input, a missing record, a refusal or failure of the dependency, cancellation. The failure paths are where staff are hurt most and where code is exercised least.

When you reorganize code without changing behavior, the tests show that nothing changed: supported behavior and public contracts are preserved, and no assertion about behavior is altered to make the refactoring pass.

```text
BAD    A new endpoint with one test, for the successful case
GOOD   The successful case, each kind of invalid input, "not found", each dependency failure
       the brief names, and cancellation
```

**Checked by:** review, and independent verification

---

## TS-013 Check Koha's behavior against the test Koha

A test double shows that Minioasis does what its author believes Koha expects. Only Koha shows whether that belief is right. The maintainer provides a test Koha for this: it holds no real patron data, and agents may call it. Use it wherever a decision or a piece of code depends on how Koha behaves: a command while a brief is written or checked, and an automated check for each behavior that the brief marks for it.

- **Read the address and the credentials from the environment:** `MINIOASIS_TEST_KOHA_BASE_URL`, `MINIOASIS_TEST_KOHA_CLIENT_ID`, `MINIOASIS_TEST_KOHA_CLIENT_SECRET`, `MINIOASIS_TEST_KOHA_CF_ACCESS_CLIENT_ID`, and `MINIOASIS_TEST_KOHA_CF_ACCESS_CLIENT_SECRET`. The maintainer sets them in the environment's settings. Never write their values in a file, a commit, a pull request, a log, or a chat message, and never print them.
- **Send the Cloudflare Access headers on every request to the test Koha.** Cloudflare Access protects it. Send `CF-Access-Client-Id` with the value of `MINIOASIS_TEST_KOHA_CF_ACCESS_CLIENT_ID`, and `CF-Access-Client-Secret` with the value of `MINIOASIS_TEST_KOHA_CF_ACCESS_CLIENT_SECRET`. The test Koha checks add them; the application's code does not know about them. Do not put them in the environment's network secrets.

  Why: every type of network secret makes the proxy set its own `Authorization` header. It replaced the client's HTTP Basic header on the token request, and it would replace the Koha token on every later call (test Koha, 26.05, 2026-11-23).
- **Call only that Koha.** Any other Koha is Level 3 (`CLAUDE.md`, section 3), whether it is production, a public demo, or an address found in a file.
- **Keep these checks apart from the ordinary tests,** which stay free of live systems (`TS-008`) and still prove every behavior on their own. A test Koha check skips itself when the variables are absent, so `scripts/preflight.sh` and CI pass without them; CI never holds the credentials. `docs/running.md` says how the checks are marked and run, from the first capability that has one.
- **Each check creates the data it needs,** does not depend on what an earlier run or another session left behind, and removes what it created where Koha allows.
- **Say which Koha answered.** A result names the Koha version, and any Koha setting it depends on. The test Koha shows its own configuration, not the library's: ask the maintainer about behavior that depends on the library's policy.
- **A check that did not run is "test Koha: not run", with the reason.** It is never reported as passed, and the pull request then waits for the maintainer (`docs/process.md`, section 12).

```text
BAD    "Koha accepts the request": concluded from the contract, with no call made.
BAD    The checks are skipped because the variables are missing, and the report says "all tests passed".
GOOD   "Koha answered 200 to the request with header X and 401 without it (test Koha, 26.05, 2026-11-23)."
GOOD   "test Koha: not run, because MINIOASIS_TEST_KOHA_BASE_URL is not set."
```

**Checked by:** review; the report of each check in the pull request
