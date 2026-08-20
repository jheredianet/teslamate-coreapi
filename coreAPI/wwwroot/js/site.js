// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.

(() => {
    const table = document.getElementById("m3u-list");
    const reorderForm = document.getElementById("m3u-reorder-form");
    const status = document.getElementById("m3u-reorder-status");

    if (!table || !reorderForm) return;

    let draggedRow = null;
    const tbody = table.querySelector("tbody");

    tbody.addEventListener("dragstart", event => {
        const row = event.target.closest("tr[data-entry-id]");
        if (!row) return;

        draggedRow = row;
        row.classList.add("dragging");
        event.dataTransfer.effectAllowed = "move";
        event.dataTransfer.setData("text/plain", row.dataset.entryId);
    });

    tbody.addEventListener("dragend", () => {
        if (draggedRow) draggedRow.classList.remove("dragging");
        draggedRow = null;
        tbody.querySelectorAll("tr.drag-over").forEach(row => row.classList.remove("drag-over"));
    });

    tbody.addEventListener("dragover", event => {
        event.preventDefault();
        const row = event.target.closest("tr[data-entry-id]");
        if (!row || row === draggedRow) return;

        tbody.querySelectorAll("tr.drag-over").forEach(item => item.classList.remove("drag-over"));
        row.classList.add("drag-over");

        const bounds = row.getBoundingClientRect();
        const insertAfter = event.clientY > bounds.top + bounds.height / 2;
        tbody.insertBefore(draggedRow, insertAfter ? row.nextSibling : row);
    });

    tbody.addEventListener("drop", async event => {
        event.preventDefault();
        if (!draggedRow) return;

        const orderedIds = [...tbody.querySelectorAll("tr[data-entry-id]")]
            .map(row => row.dataset.entryId);
        const token = reorderForm.querySelector('input[name="__RequestVerificationToken"]').value;
        const body = new URLSearchParams();
        body.append("__RequestVerificationToken", token);
        orderedIds.forEach(id => body.append("orderedIds", id));

        try {
            const response = await fetch(reorderForm.action, {
                method: "POST",
                headers: { "Content-Type": "application/x-www-form-urlencoded; charset=UTF-8" },
                body
            });

            if (!response.ok) throw new Error("No se pudo guardar el nuevo orden.");
            status.className = "alert alert-success mt-2";
            status.textContent = "Orden guardado.";
        } catch (error) {
            status.className = "alert alert-danger mt-2";
            status.textContent = error.message;
            window.setTimeout(() => window.location.reload(), 1200);
        }
    });
})();
