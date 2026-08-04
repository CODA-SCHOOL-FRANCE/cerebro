const form = document.getElementById("loginForm") as HTMLFormElement;
const errorEl = document.getElementById("loginError") as HTMLElement;
const footerYear = document.getElementById("footerYear") as HTMLElement;
const thumbprintBox = document.getElementById("thumbprintBox") as HTMLElement;
const thumbprintValue = document.getElementById("thumbprintValue") as HTMLElement;

footerYear.textContent = String(new Date().getFullYear());

form.addEventListener("submit", (event) => {
  event.preventDefault();
  submitLogin().catch((err: unknown) => {
    console.error("Connexion échouée", err);
    showError("Connexion au serveur impossible.");
  });
});

loadServerThumbprint().catch((err: unknown) => {
  // Non bloquant : absent en dehors d'un déploiement Docker + Caddy (voir Program.cs), la page de
  // connexion doit rester utilisable sans cette information.
  console.warn("Empreinte du certificat indisponible", err);
});

async function loadServerThumbprint(): Promise<void> {
  const response = await fetch("/account/server-thumbprint");
  if (!response.ok) {
    return;
  }

  const data = (await response.json()) as { thumbprint: string | null };
  if (!data.thumbprint) {
    return;
  }

  thumbprintValue.textContent = data.thumbprint;
  thumbprintBox.hidden = false;
}

async function submitLogin(): Promise<void> {
  errorEl.hidden = true;

  const formData = new FormData(form);
  const username = String(formData.get("username") ?? "");
  const password = String(formData.get("password") ?? "");

  const response = await fetch("/account/login", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ username, password })
  });

  if (!response.ok) {
    showError("Identifiant ou mot de passe incorrect.");
    return;
  }

  // ASP.NET Core (cookie auth) redirige vers /login.html?ReturnUrl=... quand un accès non
  // authentifié à une page protégée est intercepté ; on relance vers cette page une fois connecté.
  const returnUrl = new URLSearchParams(window.location.search).get("ReturnUrl");
  window.location.href = returnUrl && returnUrl.startsWith("/") ? returnUrl : "/index.html";
}

function showError(message: string): void {
  errorEl.textContent = message;
  errorEl.hidden = false;
}
