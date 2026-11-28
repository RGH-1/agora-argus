(() => {
  const field = document.querySelector("[data-rejection-field]"), form = document.querySelector("[data-rejection-form]");
  if (!field || !form) return;
  const counter = document.querySelector("#ReasonCounter"), message = document.querySelector("#ReasonMessage");
  const updateCounter = () => { const overBy = field.value.length - 500; counter.textContent = `${field.value.length}/500`; counter.classList.toggle("text-danger", overBy > 0); return overBy; };
  const validate = () => { const overBy = updateCounter(); if (overBy > 0) { field.classList.add("is-invalid"); message.textContent = `Reason is ${overBy} character${overBy === 1 ? "" : "s"} too long.`; return false; } if (!field.value.trim()) { field.classList.add("is-invalid"); message.textContent = "A rejection reason is required."; return false; } field.classList.remove("is-invalid", "input-validation-error"); message.textContent = ""; return true; };
  // A fresh form is not invalid: render its counter only. Validation begins on input or submit.
  updateCounter(); field.addEventListener("input", validate); form.addEventListener("submit", event => { if (!validate()) event.preventDefault(); });
})();
