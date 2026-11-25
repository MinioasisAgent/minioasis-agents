- **Access:** Level 2
- **Last Updated:** 2026-11-25

# Frontend

Rules `FE-001` to `FE-006`. They govern the frontend: everything under `web/`, and how it is served. The format is explained in [`README.md`](README.md). The examples use the made-up capability of that file, renewing a loan.

## FE-001 Build the frontend only with the approved tools

Each tool has one job, so no two tools compete for it. A second tool for a job that is already covered doubles what a reader must learn and what an upgrade can break.

| Tool | Its one job |
| --- | --- |
| React | Components, state, and rendering |
| TypeScript, in strict mode | Static types. No source file is plain JavaScript. |
| Vite | The development server and the production build. Vite does not check types, so the build runs the TypeScript compiler's check first and stops on a type error. |
| Tailwind CSS | Styling |
| Base UI | Accessible, unstyled interactive parts, such as a dialog or a menu. It is the only library of this kind. |
| shadcn/ui | Styled components, set up with Base UI as their base and copied into `web/` as source |
| React Router, used as a library | Mapping an address to a screen. Its framework mode is not used (`FE-003`). |
| openapi-typescript | Generating the API's types from the contract (`FE-005`) |
| openapi-fetch | Calling the API with those types |
| Vitest | Tests (`FE-006`) |
| ESLint and Prettier | Finding mistakes, and formatting |
| npm | Installing packages |

- **Any other library needs the maintainer's acceptance** (`CLAUDE.md`, section 3, Level 2, item 4). That includes a state library, a forms library, and a data-fetching library.
- **A package that an approved tool needs in order to work is still a package.** A plugin, a package that a shadcn/ui component imports, or a library that Vitest needs to test a component is listed in part 7 of the brief, or in the Level 2 pull request that adds it, like any other (`docs/process.md`, section 6).
- **`web/package.json` and its lock file are the one place for versions.** A major version upgrade is Level 2.
- **A shadcn/ui component is our source code once it is copied in.** Editing it is normal work. The generated API types are generated code and are never edited by hand.

**Checked by:** the frontend's build, which fails on a type error, and review

---

## FE-002 Keep the frontend thin: it shows, collects, and calls

The backend owns every library rule and every check of one (`CLAUDE.md`, section 1, rule 1). A rule that the frontend also holds becomes a second copy, and the two drift apart. The frontend also runs in a browser, where anyone can change it, so nothing it checks can be trusted.

- The frontend may refuse to send a form whose required field is empty. It makes no other check of its own.
- It shows the backend's error text as the backend returns it.
- Everything it shows is in English. It holds no translations and no language setting.
- It never calls Koha, and it holds no secret.

```tsx
// BAD: the frontend decides a library rule
if (loan.renewalCount >= 3) {
  return <p>This loan cannot be renewed again.</p>;
}

// GOOD: the frontend asks, and shows the backend's answer
async function renew() {
  const { error, response } = await api.POST("/api/v1/loans/{id}/renewal", {
    params: { path: { id } },
  });
  if (!response.ok) {
    setProblem(error);
  }
}

// The backend's title is always there. Its detail is not (`ER-006`).
{problem && (
  <div role="alert">
    <p>{problem.title}</p>
    {problem.detail && <p>{problem.detail}</p>}
  </div>
)}
```

---

## FE-003 Ship static files only

Behind Cloudflare, Minioasis runs as two programs: nginx and the backend. A program that renders pages on a server is a third one to secure, start, and watch, and it gives rules a second place to live.

- No server-side rendering, and no full-stack JavaScript framework, such as Next.js or the framework mode of React Router.
- No Node.js process runs in production. Node.js is a tool on a developer's machine and in CI.
- No layer of our own stands between the frontend and the backend.

```text
BAD:  browser ──► Node.js server that renders pages ──► backend
GOOD: browser ──► nginx ──► static files
                        └─► backend, for /api/
```

**Checked by:** the frontend's build, whose whole output is static files, and review

---

## FE-004 Serve the frontend and the API through nginx on one address

With one address, the browser needs no cross-origin permission, the backend needs no CORS setting, and the frontend never handles a token.

There are two boundaries, and neither relies on the other.

- **Cloudflare Access is the first.** It protects the whole staff address: the frontend's HTML, CSS, JavaScript, and other static files, and every route under `/api/`. No path is left out. A person who is not signed in cannot load the React application at all.
- **The backend is the second.** It validates Cloudflare's token and the staff member's email itself, on every request, as defence in depth against a request that bypasses the first (`docs/briefs/002-staff-access-control.md`).

The frontend is no boundary. It checks no sign-in and holds no token.

How a request travels in production. The backend's part of step 6 holds once staff access control is built (`docs/ROADMAP.md`).

1. The staff member's browser opens the Minioasis address over HTTPS. Cloudflare Access signs the staff member in before anything reaches the server, whether the request is for a file of the frontend or for the API.
2. Cloudflare Tunnel passes each request to nginx. Nginx listens on localhost only.
3. Nginx serves the built frontend files. For an address that is not a file and does not start with `/api/`, it serves `index.html`, so that React Router can show the right screen.
4. The frontend calls the API (`FE-005`) on relative paths that start with `/api/`. It sends no token and stores none.
5. Cloudflare Access adds a signed token to every request that it lets through. Nginx passes requests under `/api/` to the backend, with that token unchanged.
6. The backend is reachable from its own machine only. It validates the token and the email itself on every request, does the work, and answers in JSON.
7. The answer returns through nginx and Cloudflare to the browser, and React updates the screen.

When the sign-in ends while a screen is open, Cloudflare Access answers an API call itself. By default it answers with a redirect to its sign-in page, which the frontend cannot tell from a network failure. So the frontend sends the header `X-Requested-With: XMLHttpRequest` on every API call. By Cloudflare's documentation ("Session management", section "AJAX"), Access then answers 401. The backend requires the same header on every call, and refuses a call without it (`docs/briefs/002-staff-access-control.md`).

The backend answers 401 too, as Problem Details, when it cannot confirm the token. On a 401 from either, the frontend reloads the page one time, so that Cloudflare Access can sign the staff member in again. If the first API call after that reload is also answered 401, the frontend does not reload again: it shows the backend's error when the 401 is Problem Details, and otherwise asks the staff member to sign in again.

Why one time: a wrong setting can make the backend answer 401 while Cloudflare Access still sees a signed-in person, and a reload on every 401 would then never stop.

In development, Vite's development server takes the place of nginx and passes `/api/` to the backend.

```nginx
# The shape of the nginx configuration. The ports and the folder are examples.
server {
    listen 127.0.0.1:8080;
    root /var/www/minioasis;

    location /api/ {
        proxy_pass http://127.0.0.1:5000;
    }

    location / {
        try_files $uri /index.html;
    }
}
```

```ts
// BAD: an absolute address, and a token that the frontend handles
fetch("https://minioasis.example/api/v1/loans/1", {
  headers: { Authorization: `Bearer ${token}` },
});

// GOOD: a relative path, and no token
api.GET("/api/v1/loans/{id}", { params: { path: { id } } });
```

---

## FE-005 Take the API's types from the contract

The API contract (`docs/process.md`, section 3) is the one description of the API that both components build from. Types written by hand are a second description, and they drift from the backend without any check failing.

- Generate the types from the contract with openapi-typescript. Regenerate them when the contract changes.
- Call the API only through the openapi-fetch client that uses those types.
- Never change the contract to suit the frontend. If the contract is wrong or incomplete, stop and report it (`docs/process.md`, section 6).

```ts
// BAD: a type written by hand
type Loan = { id: number; dueDate: string };
const loan = (await (await fetch(`/api/v1/loans/${id}`)).json()) as Loan;

// GOOD: the type comes from the contract, so a wrong route or field fails the build
const { data: loan } = await api.GET("/api/v1/loans/{id}", {
  params: { path: { id } },
});
```

**Checked by:** the frontend's build, and review

---

## FE-006 Test only the logic that the frontend owns

A thin frontend has little logic. A test that only repeats what a component renders costs more to keep than it catches. What staff see is checked once, against the running backend, in the joint check (`docs/process.md`, section 8).

- Write a test where the frontend has logic of its own: a function that formats a date, or a component that chooses between two states.
- Write no test for a component that only shows what it is given.
- The rules of [`testing.md`](testing.md) apply to the backend. They do not apply under `web/`.

```tsx
// BAD: the test repeats what the component renders
test("shows the barcode", () => {
  render(<Barcode value="MO-1" />);
  expect(screen.getByText("MO-1")).toBeVisible();
});

// GOOD: logic that the frontend owns has a test
test("formats a due date for staff", () => {
  expect(formatDueDate("2026-12-01")).toBe("1 December 2026");
});
```
