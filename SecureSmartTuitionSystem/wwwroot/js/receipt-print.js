(() => {
    const applyTitle = () => {
        const name = document.getElementById("printReceiptButton")?.dataset.receiptFileName;
        if (name) document.title = name;
    };
    // site.js binds the existing printReceiptButton and calls this function.
    window.printReceipt = () => { applyTitle(); window.print(); };
    window.addEventListener("beforeprint", applyTitle);
    if (document.readyState === "loading") document.addEventListener("DOMContentLoaded", applyTitle);
    else applyTitle();
})();
