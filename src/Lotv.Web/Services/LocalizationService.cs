using Microsoft.JSInterop;

namespace Lotv.Web.Services;

public class LocalizationService
{
    private readonly IJSRuntime _js;
    private string _currentCulture = "en";

    public event Action? OnCultureChanged;

    public string CurrentCulture => _currentCulture;

    public IReadOnlyList<(string Code, string Name)> AvailableCultures { get; } = new[]
    {
        ("en", "English"),
        ("es", "Español"),
    };

    public LocalizationService(IJSRuntime js)
    {
        _js = js;
    }

    public async Task InitializeAsync()
    {
        try
        {
            var stored = await _js.InvokeAsync<string?>("localStorage.getItem", "lotv.culture");
            if (!string.IsNullOrWhiteSpace(stored) && Strings.ContainsKey(stored))
            {
                _currentCulture = stored!;
            }
        }
        catch { /* prerender / no JS */ }
    }

    public async Task SetCultureAsync(string culture)
    {
        if (!Strings.ContainsKey(culture) || _currentCulture == culture) return;
        _currentCulture = culture;
        try { await _js.InvokeVoidAsync("localStorage.setItem", "lotv.culture", culture); }
        catch { /* ignore */ }
        OnCultureChanged?.Invoke();
    }

    public string this[string key] => T(key);

    public string T(string key)
    {
        if (Strings.TryGetValue(_currentCulture, out var dict) && dict.TryGetValue(key, out var v))
            return v;
        if (Strings["en"].TryGetValue(key, out var fallback))
            return fallback;
        return key;
    }

    private static readonly Dictionary<string, Dictionary<string, string>> Strings = new()
    {
        ["en"] = new()
        {
            // Layout / nav
            ["nav.home"]            = "Home",
            ["nav.gethelp"]         = "Get Help",
            ["nav.donate"]          = "Donate",
            ["nav.volunteer"]       = "Volunteer",
            ["nav.events"]          = "Events",
            ["nav.impact"]          = "Our Impact",
            ["nav.staffLogin"]      = "Staff Login",
            ["nav.staffPortal"]     = "Staff Portal",
            ["layout.tagline"]      = "Catholic Apostolate",
            ["layout.footerTitle"]  = "Lily of the Valley Ministry",
            ["layout.footerBlurb"]  = "A Catholic Apostolate supporting families through pregnancy and infant loss",
            ["layout.footerCredit"] = "LOTV Catholic Apostolate • Built by WTE Solutions",
            ["lang.label"]          = "Language",

            // Home
            ["home.eyebrow"]        = "A Catholic Apostolate",
            ["home.heroLine1"]      = "You are not alone",
            ["home.heroLine2"]      = "in your grief.",
            ["home.heroBlurb"]      = "Lily of the Valley Ministry walks alongside families experiencing pregnancy loss, infant loss, and infertility — offering comfort packages, prayer, and community rooted in Catholic faith.",
            ["home.ctaRequest"]     = "Request a Comfort Package",
            ["home.ctaSupport"]     = "Support Our Mission",
            ["home.kpiFamilies"]    = "Families Served",
            ["home.kpiPackages"]    = "Packages Fulfilled",
            ["home.kpiVolunteers"]  = "Active Volunteers",
            ["home.kpiDioceses"]    = "Dioceses Reached",
            ["home.howTitle"]       = "How We Help",
            ["home.howBlurb"]       = "Whether you are grieving a recent loss or seeking support after years of suffering, we are here for you.",
            ["home.cardPackTitle"]  = "Comfort Packages",
            ["home.cardPackBody"]   = "Hand-assembled packages with memory items, prayer resources, and a personalized bracelet with your baby's initials.",
            ["home.cardPackLink"]   = "Request yours →",
            ["home.cardPrayTitle"]  = "Prayer & Community",
            ["home.cardPrayBody"]   = "Our prayer ambassadors commit to praying for each family by name. You are remembered and loved.",
            ["home.cardPrayLink"]   = "Become a prayer ambassador →",
            ["home.cardParTitle"]   = "Parish Network",
            ["home.cardParBody"]    = "We partner with parishes across multiple dioceses to ensure every grieving family is reached — regardless of location.",
            ["home.cardParLink"]    = "Support the network →",
            ["home.serveTitle"]     = "Who We Serve",
            ["home.serveBlurb"]     = "Our ministry is open to all families regardless of faith background who have experienced:",
            ["home.tag.miscarriage"] = "Miscarriage",
            ["home.tag.stillbirth"]  = "Stillbirth",
            ["home.tag.prenatal"]    = "Prenatal Diagnosis",
            ["home.tag.lifeLimit"]   = "Prenatal Life-Limiting Diagnosis",
            ["home.tag.infant"]      = "Infant Loss",
            ["home.tag.infertility"] = "Infertility",
            ["home.tag.past"]        = "Past Loss",
            ["home.giTitle"]        = "Get Involved",
            ["home.giDonateTitle"]  = "Make a Donation",
            ["home.giDonateBody"]   = "Every gift directly funds a comfort package for a grieving family.",
            ["home.giDonateBtn"]    = "Donate Now",
            ["home.giVolTitle"]     = "Volunteer",
            ["home.giVolBody"]      = "Assemble packages, pray for families, or serve as a parish liaison.",
            ["home.giVolBtn"]       = "Sign Up",

            // Common form / buttons
            ["form.firstName"]      = "First name",
            ["form.lastName"]       = "Last name",
            ["form.email"]          = "Email",
            ["form.phone"]          = "Phone",
            ["form.address"]        = "Address",
            ["form.city"]           = "City",
            ["form.state"]          = "State",
            ["form.zip"]            = "ZIP",
            ["form.notes"]          = "Notes",
            ["btn.submit"]          = "Submit",
            ["btn.cancel"]          = "Cancel",
            ["btn.save"]            = "Save",
            ["btn.back"]            = "Back",
            ["btn.next"]            = "Next",
            ["msg.thanks"]          = "Thank you",
            ["msg.required"]        = "This field is required.",
            ["msg.error"]           = "Something went wrong. Please try again.",
        },
        ["es"] = new()
        {
            // Layout / nav
            ["nav.home"]            = "Inicio",
            ["nav.gethelp"]         = "Pedir Ayuda",
            ["nav.donate"]          = "Donar",
            ["nav.volunteer"]       = "Voluntariado",
            ["nav.events"]          = "Eventos",
            ["nav.impact"]          = "Nuestro Impacto",
            ["nav.staffLogin"]      = "Acceso Personal",
            ["nav.staffPortal"]     = "Portal del Personal",
            ["layout.tagline"]      = "Apostolado Católico",
            ["layout.footerTitle"]  = "Ministerio Lily of the Valley",
            ["layout.footerBlurb"]  = "Un apostolado católico que apoya a familias en la pérdida prenatal e infantil",
            ["layout.footerCredit"] = "LOTV Apostolado Católico • Construido por WTE Solutions",
            ["lang.label"]          = "Idioma",

            // Home
            ["home.eyebrow"]        = "Un Apostolado Católico",
            ["home.heroLine1"]      = "No estás solo",
            ["home.heroLine2"]      = "en tu dolor.",
            ["home.heroBlurb"]      = "El Ministerio Lily of the Valley acompaña a las familias que han sufrido pérdida prenatal, pérdida infantil e infertilidad — ofreciendo paquetes de consuelo, oración y comunidad arraigada en la fe católica.",
            ["home.ctaRequest"]     = "Solicitar un Paquete de Consuelo",
            ["home.ctaSupport"]     = "Apoye Nuestra Misión",
            ["home.kpiFamilies"]    = "Familias Atendidas",
            ["home.kpiPackages"]    = "Paquetes Entregados",
            ["home.kpiVolunteers"]  = "Voluntarios Activos",
            ["home.kpiDioceses"]    = "Diócesis Alcanzadas",
            ["home.howTitle"]       = "Cómo Ayudamos",
            ["home.howBlurb"]       = "Ya sea que esté de luto por una pérdida reciente o buscando apoyo después de años de sufrimiento, estamos aquí para usted.",
            ["home.cardPackTitle"]  = "Paquetes de Consuelo",
            ["home.cardPackBody"]   = "Paquetes ensamblados a mano con artículos conmemorativos, recursos de oración y una pulsera personalizada con las iniciales de su bebé.",
            ["home.cardPackLink"]   = "Solicite el suyo →",
            ["home.cardPrayTitle"]  = "Oración y Comunidad",
            ["home.cardPrayBody"]   = "Nuestros embajadores de oración se comprometen a orar por cada familia por su nombre. Usted es recordado y amado.",
            ["home.cardPrayLink"]   = "Sea un embajador de oración →",
            ["home.cardParTitle"]   = "Red Parroquial",
            ["home.cardParBody"]    = "Nos asociamos con parroquias en múltiples diócesis para asegurar que cada familia en duelo sea alcanzada — sin importar la ubicación.",
            ["home.cardParLink"]    = "Apoye la red →",
            ["home.serveTitle"]     = "A Quién Servimos",
            ["home.serveBlurb"]     = "Nuestro ministerio está abierto a todas las familias, sin importar su trasfondo de fe, que han experimentado:",
            ["home.tag.miscarriage"] = "Aborto espontáneo",
            ["home.tag.stillbirth"]  = "Mortinato",
            ["home.tag.prenatal"]    = "Diagnóstico prenatal",
            ["home.tag.lifeLimit"]   = "Diagnóstico prenatal limitante",
            ["home.tag.infant"]      = "Pérdida infantil",
            ["home.tag.infertility"] = "Infertilidad",
            ["home.tag.past"]        = "Pérdida pasada",
            ["home.giTitle"]        = "Participe",
            ["home.giDonateTitle"]  = "Haga una Donación",
            ["home.giDonateBody"]   = "Cada regalo financia directamente un paquete de consuelo para una familia en duelo.",
            ["home.giDonateBtn"]    = "Donar Ahora",
            ["home.giVolTitle"]     = "Voluntariado",
            ["home.giVolBody"]      = "Ensamble paquetes, ore por familias o sirva como enlace parroquial.",
            ["home.giVolBtn"]       = "Inscríbase",

            // Common form / buttons
            ["form.firstName"]      = "Nombre",
            ["form.lastName"]       = "Apellido",
            ["form.email"]          = "Correo electrónico",
            ["form.phone"]          = "Teléfono",
            ["form.address"]        = "Dirección",
            ["form.city"]           = "Ciudad",
            ["form.state"]          = "Estado",
            ["form.zip"]            = "Código postal",
            ["form.notes"]          = "Notas",
            ["btn.submit"]          = "Enviar",
            ["btn.cancel"]          = "Cancelar",
            ["btn.save"]            = "Guardar",
            ["btn.back"]            = "Atrás",
            ["btn.next"]            = "Siguiente",
            ["msg.thanks"]          = "Gracias",
            ["msg.required"]        = "Este campo es obligatorio.",
            ["msg.error"]           = "Algo salió mal. Por favor inténtelo de nuevo.",
        },
    };
}
