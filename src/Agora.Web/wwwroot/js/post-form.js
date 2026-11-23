(() => {
  const updateField = (field) => {
    const limit = Number(field.dataset.limit);
    const label = field.dataset.label;
    const length = field.value.length;
    const overBy = length - limit;
    const counter = document.querySelector(`[data-counter-for="${field.id}"]`);
    const message = document.querySelector(`[data-length-message-for="${field.id}"]`);
    if (counter) {
      counter.textContent = `${length}/${limit}`;
      counter.classList.toggle("text-danger", overBy > 0);
    }
    if (overBy > 0) {
      field.classList.add("is-invalid");
      if (message) message.textContent = `${label} is ${overBy} character${overBy === 1 ? "" : "s"} too long.`;
      return false;
    }
    if (!field.value.trim() && field.dataset.optional !== "true") {
      field.classList.add("is-invalid");
      if (message) message.textContent = `${label} is required.`;
      return false;
    }
    field.classList.remove("is-invalid");
    if (message) message.textContent = "";
    return true;
  };

  document.querySelectorAll("[data-post-form], [data-length-form]").forEach((form) => {
    const fields = [...form.querySelectorAll("[data-post-field], [data-length-field]")];
    fields.forEach((field) => {
      updateField(field);
      field.addEventListener("input", () => updateField(field));
    });
    form.addEventListener("submit", (event) => {
      if (!fields.map(updateField).every(Boolean)) event.preventDefault();
    });
  });
})();
