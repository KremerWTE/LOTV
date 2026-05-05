// LOTV — small browser helpers exposed to Blazor via JSInterop.

window.lotvTheme = {
  get: function () {
    try { return localStorage.getItem('lotv.theme') || 'light'; } catch { return 'light'; }
  },
  set: function (theme) {
    try { localStorage.setItem('lotv.theme', theme); } catch { }
    document.body.classList.remove('theme-light', 'theme-dark');
    document.body.classList.add('theme-' + theme);
  },
  init: function () {
    this.set(this.get());
  }
};

// Toggle .scrolled on .topbar when its sibling .lotv-content scrolls. Reattaches each route nav.
window.lotvScrollShadow = {
  attach: function () {
    document.querySelectorAll('.lotv-content').forEach(function (c) {
      if (c.dataset.shadowAttached) return;
      c.dataset.shadowAttached = '1';
      var topbar = document.querySelector('.topbar');
      if (!topbar) return;
      c.addEventListener('scroll', function () {
        topbar.classList.toggle('scrolled', c.scrollTop > 4);
      });
    });
  }
};

window.lotvUpload = {
  click: function (inputElement) { try { inputElement.click(); } catch { } },
  // Returns base64 data-URL of the first selected file in inputElement.
  readDataUrl: function (inputElement) {
    return new Promise(function (resolve, reject) {
      try {
        var file = inputElement.files && inputElement.files[0];
        if (!file) { resolve(null); return; }
        var r = new FileReader();
        r.onload = function () { resolve(r.result); };
        r.onerror = function () { reject(r.error); };
        r.readAsDataURL(file);
      } catch (e) { reject(e); }
    });
  }
};

window.lotvPush = {
  isSupported: function () { return 'serviceWorker' in navigator && 'PushManager' in window; },
  registerServiceWorker: async function () {
    try { return !!(await navigator.serviceWorker.register('/sw.js')); }
    catch (e) { console.warn('SW register failed', e); return false; }
  },
  subscribe: async function (vapidPublicKey) {
    if (!this.isSupported()) return null;
    var reg = await navigator.serviceWorker.ready;
    var perm = await Notification.requestPermission();
    if (perm !== 'granted') return null;
    var sub = await reg.pushManager.subscribe({
      userVisibleOnly: true,
      applicationServerKey: urlBase64ToUint8Array(vapidPublicKey)
    });
    return JSON.stringify(sub);
  }
};

function urlBase64ToUint8Array(base64) {
  var padding = '='.repeat((4 - base64.length % 4) % 4);
  var b64 = (base64 + padding).replace(/-/g, '+').replace(/_/g, '/');
  var raw = atob(b64);
  var out = new Uint8Array(raw.length);
  for (var i = 0; i < raw.length; ++i) out[i] = raw.charCodeAt(i);
  return out;
}

// Stripe Elements thin wrapper. Caller passes publishable key + client secret.
window.lotvStripe = {
  _stripe: null,
  _elements: null,
  _card: null,
  init: async function (publishableKey) {
    if (!window.Stripe) {
      await new Promise(function (resolve) {
        var s = document.createElement('script');
        s.src = 'https://js.stripe.com/v3/';
        s.onload = resolve; document.head.appendChild(s);
      });
    }
    this._stripe = Stripe(publishableKey);
  },
  mountCard: function (mountSelector, clientSecret) {
    this._elements = this._stripe.elements({ clientSecret: clientSecret });
    this._card = this._elements.create('payment');
    this._card.mount(mountSelector);
  },
  confirm: async function (returnUrl) {
    var result = await this._stripe.confirmPayment({
      elements: this._elements,
      confirmParams: { return_url: returnUrl },
      redirect: 'if_required'
    });
    if (result.error) return { ok: false, error: result.error.message };
    return { ok: true, paymentIntentId: result.paymentIntent ? result.paymentIntent.id : null };
  }
};
