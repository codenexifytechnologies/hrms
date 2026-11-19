/* Opens Razorpay Checkout from #rzpOptions and posts the signed result to the server for verification. */
(function () {
  "use strict";
  const button = document.querySelector("[data-rzp-open]");
  const form = document.querySelector("[data-rzp-form]");
  const errorBox = document.querySelector("[data-rzp-error]");
  const src = document.getElementById("rzpOptions");
  if (!button || !form || !src) return;

  function showError(message) {
    errorBox.textContent = message;
    errorBox.classList.remove("d-none");
    button.disabled = false;
  }

  if (typeof window.Razorpay !== "function") {
    showError("The payment window could not be loaded. Check your connection or pay by bank transfer.");
    button.disabled = true;
    return;
  }

  const options = JSON.parse(src.textContent);
  options.theme = { color: "#4f46e5" };
  options.handler = function (response) {
    form.elements["razorpay_order_id"].value = response.razorpay_order_id || "";
    form.elements["razorpay_payment_id"].value = response.razorpay_payment_id || "";
    form.elements["razorpay_signature"].value = response.razorpay_signature || "";
    form.submit();
  };
  options.modal = { ondismiss: function () { button.disabled = false; } };

  const rzp = new window.Razorpay(options);
  rzp.on("payment.failed", function (r) {
    showError((r && r.error && r.error.description) || "The payment failed. You were not charged; please try again.");
  });
  button.addEventListener("click", function () {
    button.disabled = true;
    errorBox.classList.add("d-none");
    rzp.open();
  });
})();
