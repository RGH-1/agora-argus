(() => {
  const field = document.querySelector("[data-rejection-field]"), form = document.querySelector("[data-rejection-form]");
  if (!field || !form) return;
  const counter = document.querySelector("#ReasonCounter"), message = document.querySelector("#ReasonMessage");
  const validate = () => { const overBy = field.value.length - 500; counter.textContent = `${field.value.length}/500`; counter.classList.toggle("text-danger", overBy > 0); if (overBy > 0) { field.classList.add("is-invalid"); message.textContent = `Reason is ${overBy} character${overBy === 1 ? "" : "s"} too long.`; return false; } if (!field.value.trim()) { field.classList.add("is-invalid"); message.textContent = "A rejection reason is required."; return false; } field.classList.remove("is-invalid"); message.textContent = ""; return true; };
  validate(); field.addEventListener("input", validate); form.addEventListener("submit", event => { if (!validate()) event.preventDefault(); });
})();
