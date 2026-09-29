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

            // Apply
            ["apply.title"]          = "Request a Comfort Package",
            ["apply.subtitle"]       = "We're so sorry for your loss. Please share a bit about your situation so we can send a package that fits your needs.",
            ["apply.forMe"]          = "For me",
            ["apply.forSomeoneElse"] = "For someone else",
            ["apply.submit"]         = "Submit Request",
            // Give
            ["give.title"]           = "Support a Grieving Family",
            ["give.subtitle"]        = "Every gift funds a hand-assembled comfort package delivered free of charge to a family navigating pregnancy or infant loss.",
            ["give.amount"]          = "Gift Amount",
            ["give.giveOnce"]        = "Give Now",
            ["give.giveMonthly"]     = "Set Up Monthly Gift",
            ["give.donorNote"]       = "LOTV Ministry is a 501(c)(3) non-profit organization.",
            // Volunteer signup
            ["vol.title"]            = "Become a Volunteer",
            ["vol.subtitle"]         = "Assemble packages, pray for families, or serve as a parish liaison.",
            ["vol.signup"]           = "Sign Up",
            ["vol.heroTitle"]        = "Join Our Mission",
            ["vol.heroBlurb"]        = "Volunteers are the heart of LOTV Ministry. From assembling packages to praying for families, every role makes a direct difference in someone's grief journey.",
            ["vol.roleHint"]         = "Click a role above, or choose one from the dropdown below — both set the same \"Area of Interest.\"",
            ["vol.sectionYourInfo"]  = "Your Information",
            ["vol.sectionParishRole"] = "Parish & Role",
            ["vol.sectionAboutYou"]  = "About You",
            ["vol.parishName"]       = "Parish Name",
            ["vol.areaOfInterest"]   = "Area of Interest",
            ["vol.whyVolunteer"]     = "Why do you want to volunteer?",
            ["vol.bioPlaceholder"]   = "Share your motivation or connection to this mission...",
            ["vol.submitBtn"]        = "Submit Volunteer Application",
            ["vol.role.packageAssembler.title"] = "Package Assembler",
            ["vol.role.packageAssembler.desc"]  = "Assemble and pack comfort boxes with love and care.",
            ["vol.role.prayerAmbassador.title"] = "Prayer Ambassador",
            ["vol.role.prayerAmbassador.desc"]  = "Commit to praying daily for assigned families by name.",
            ["vol.role.parishLiaison.title"]    = "Parish Liaison",
            ["vol.role.parishLiaison.desc"]     = "Coordinate between LOTV and your local parish community.",
            ["vol.role.eventHelper.title"]      = "Event Helper",
            ["vol.role.eventHelper.desc"]       = "Support galas, prayer nights, and community gatherings.",
            ["vol.role.driver.title"]           = "Driver",
            ["vol.role.driver.desc"]            = "Deliver packages locally when families cannot receive by mail.",
            ["vol.role.admin.title"]            = "Admin Support",
            ["vol.role.admin.desc"]             = "Help with case coordination, data entry, and communications.",
            ["vol.thanksTitle"]      = "Welcome to the LOTV family.",
            ["vol.thanksBody"]       = "Thank you for your desire to serve. Our volunteer coordinator will be in touch soon to complete your onboarding.",
            ["vol.signedUpAs"]       = "You signed up as:",
            ["vol.statusOnboarding"] = "Status: Onboarding — we'll be in touch shortly",
            ["vol.returnHome"]       = "Return Home",
            ["vol.errFirstLast"]     = "Please enter your first and last name.",
            ["vol.errEmail"]         = "Please enter your email address.",
            ["vol.errSubmit"]        = "Unable to submit your application. Please try again.",
            // Help / FAQ
            ["help.title"]           = "Help & FAQ",
            ["help.searchPlaceholder"] = "Search the FAQ…",
            // Transparency
            ["trans.title"]          = "Our Impact",
            ["trans.subtitle"]       = "Where every gift goes — published openly and updated continuously.",
            ["trans.heroTitle"]      = "Our Impact & Transparency Report",
            ["trans.heroBlurb"]      = "We believe in full transparency. Here is a real-time view of where donations go and how we are serving families in need — no PII, aggregate data only.",
            ["trans.loading"]        = "Loading impact data…",
            ["trans.kpiTotalDonated"] = "Total Donated",
            ["trans.kpiPeopleHelped"] = "People Helped",
            ["trans.kpiRequestsFulfilled"] = "Requests Fulfilled",
            ["trans.kpiActiveVolunteers"] = "Active Volunteers",
            ["trans.whereGoes"]      = "Where Donations Go",
            ["trans.noAllocation"]   = "No allocation data available yet.",
            ["trans.casesSuffix"]    = "cases",
            ["trans.monthlyActivity"] = "Monthly Activity (Last 6 Months)",
            ["trans.colMonth"]       = "Month",
            ["trans.colDonations"]   = "Donations Received",
            ["trans.colFamilies"]    = "Families Served",
            ["trans.colNewRequests"] = "New Requests",
            ["trans.missionTitle"]   = "Our Mission",
            ["trans.missionBody"]    = "LOTV Ministry is a Catholic apostolate dedicated to serving families in need through coordinated distribution of food, clothing, shelter assistance, and other essential resources. Every dollar donated goes directly to serving our neighbors.",
            ["trans.ctaDonateTitle"] = "Make a Donation",
            ["trans.ctaDonateBody"]  = "Support families in your community",
            ["trans.ctaVolunteerTitle"] = "Become a Volunteer",
            ["trans.ctaVolunteerBody"]  = "Give your time and talents",
            ["trans.ctaApplyTitle"]  = "Apply for Assistance",
            ["trans.ctaApplyBody"]   = "We are here to help",
            ["trans.footerNote"]     = "Data updated in real time. All figures are aggregate totals — no personally identifiable information is displayed.",
            ["trans.staffLogin"]     = "Staff login",
            // Events
            ["events.title"]         = "Upcoming Events",
            ["events.past"]          = "Past Events",
            ["events.rsvp"]          = "RSVP / Buy Tickets",
            ["events.heroBlurb"]     = "Join us for galas, silent auctions, and community gatherings. Every event supports families in need.",
            ["events.loading"]       = "Loading events…",
            ["events.emptyTitle"]    = "No Upcoming Events",
            ["events.emptyBody"]     = "Check back soon — events are being planned!",
            ["events.virtual"]       = "Virtual",
            ["events.inPerson"]      = "In-Person",
            ["events.date"]          = "Date",
            ["events.location"]      = "Location",
            ["events.online"]        = "Online",
            ["events.tbd"]           = "TBD",
            ["events.registered"]    = "registered",
            ["events.spotsTotal"]    = "spots total",
            ["events.full"]          = "This event is full",
            ["events.almostFull"]    = "⚠ Almost full — {0} spots left",
            ["events.eventFull"]     = "Event Full",
            ["events.rsvpRegister"]  = "RSVP / Register",
            ["events.attended"]      = "attended",
            ["events.modalYourName"] = "Your Name",
            ["events.modalEmail"]    = "Email",
            ["events.modalGuests"]   = "Number of guests",
            ["events.modalConfirm"]  = "Confirm Registration",
            ["events.modalClose"]    = "Close",
            ["events.modalConfirmed"] = "✓ You are registered! A confirmation will be sent to {0}.",
            ["events.errName"]       = "Name is required.",
            ["events.errEmail"]      = "Email is required.",
            ["events.errSubmit"]     = "Unable to complete registration. Please try again.",
            ["common.optional"]      = "(optional)",
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

            // Apply
            ["apply.title"]          = "Solicitar un Paquete de Consuelo",
            ["apply.subtitle"]       = "Lamentamos mucho su pérdida. Por favor cuéntenos un poco sobre su situación para que podamos enviarle un paquete que se ajuste a sus necesidades.",
            ["apply.forMe"]          = "Para mí",
            ["apply.forSomeoneElse"] = "Para otra persona",
            ["apply.submit"]         = "Enviar solicitud",
            // Give
            ["give.title"]           = "Apoye a una Familia en Duelo",
            ["give.subtitle"]        = "Cada regalo financia un paquete de consuelo ensamblado a mano, entregado gratuitamente a una familia que enfrenta una pérdida prenatal o infantil.",
            ["give.amount"]          = "Monto del Regalo",
            ["give.giveOnce"]        = "Donar Ahora",
            ["give.giveMonthly"]     = "Configurar Donación Mensual",
            ["give.donorNote"]       = "LOTV Ministry es una organización sin fines de lucro 501(c)(3).",
            // Volunteer signup
            ["vol.title"]            = "Conviértase en Voluntario",
            ["vol.subtitle"]         = "Ensamble paquetes, ore por familias o sirva como enlace parroquial.",
            ["vol.signup"]           = "Inscribirse",
            ["vol.heroTitle"]        = "Únase a Nuestra Misión",
            ["vol.heroBlurb"]        = "Los voluntarios son el corazón del Ministerio LOTV. Desde ensamblar paquetes hasta orar por familias, cada función marca una diferencia directa en el proceso de duelo de alguien.",
            ["vol.roleHint"]         = "Haga clic en un rol arriba, o elija uno en el menú desplegable a continuación — ambos establecen la misma \"Área de Interés\".",
            ["vol.sectionYourInfo"]  = "Su Información",
            ["vol.sectionParishRole"] = "Parroquia y Rol",
            ["vol.sectionAboutYou"]  = "Sobre Usted",
            ["vol.parishName"]       = "Nombre de la Parroquia",
            ["vol.areaOfInterest"]   = "Área de Interés",
            ["vol.whyVolunteer"]     = "¿Por qué quiere ser voluntario?",
            ["vol.bioPlaceholder"]   = "Comparta su motivación o conexión con esta misión...",
            ["vol.submitBtn"]        = "Enviar Solicitud de Voluntariado",
            ["vol.role.packageAssembler.title"] = "Ensamblador de Paquetes",
            ["vol.role.packageAssembler.desc"]  = "Ensamble y empaque cajas de consuelo con amor y cuidado.",
            ["vol.role.prayerAmbassador.title"] = "Embajador de Oración",
            ["vol.role.prayerAmbassador.desc"]  = "Comprométase a orar diariamente por las familias asignadas, por su nombre.",
            ["vol.role.parishLiaison.title"]    = "Enlace Parroquial",
            ["vol.role.parishLiaison.desc"]     = "Coordine entre LOTV y su comunidad parroquial local.",
            ["vol.role.eventHelper.title"]      = "Ayudante de Eventos",
            ["vol.role.eventHelper.desc"]       = "Apoye galas, noches de oración y reuniones comunitarias.",
            ["vol.role.driver.title"]           = "Conductor",
            ["vol.role.driver.desc"]            = "Entregue paquetes localmente cuando las familias no puedan recibirlos por correo.",
            ["vol.role.admin.title"]            = "Apoyo Administrativo",
            ["vol.role.admin.desc"]             = "Ayude con la coordinación de casos, entrada de datos y comunicaciones.",
            ["vol.thanksTitle"]      = "Bienvenido a la familia LOTV.",
            ["vol.thanksBody"]       = "Gracias por su deseo de servir. Nuestro coordinador de voluntarios se pondrá en contacto pronto para completar su incorporación.",
            ["vol.signedUpAs"]       = "Se inscribió como:",
            ["vol.statusOnboarding"] = "Estado: En incorporación — nos pondremos en contacto pronto",
            ["vol.returnHome"]       = "Volver al Inicio",
            ["vol.errFirstLast"]     = "Por favor ingrese su nombre y apellido.",
            ["vol.errEmail"]         = "Por favor ingrese su correo electrónico.",
            ["vol.errSubmit"]        = "No se pudo enviar su solicitud. Por favor inténtelo de nuevo.",
            // Help / FAQ
            ["help.title"]           = "Ayuda y Preguntas Frecuentes",
            ["help.searchPlaceholder"] = "Buscar en las preguntas frecuentes…",
            // Transparency
            ["trans.title"]          = "Nuestro Impacto",
            ["trans.subtitle"]       = "Adónde va cada donación — publicado abiertamente y actualizado continuamente.",
            ["trans.heroTitle"]      = "Nuestro Impacto e Informe de Transparencia",
            ["trans.heroBlurb"]      = "Creemos en la transparencia total. Aquí hay una vista en tiempo real de adónde van las donaciones y cómo estamos sirviendo a las familias necesitadas — sin información personal, solo datos agregados.",
            ["trans.loading"]        = "Cargando datos de impacto…",
            ["trans.kpiTotalDonated"] = "Total Donado",
            ["trans.kpiPeopleHelped"] = "Personas Ayudadas",
            ["trans.kpiRequestsFulfilled"] = "Solicitudes Cumplidas",
            ["trans.kpiActiveVolunteers"] = "Voluntarios Activos",
            ["trans.whereGoes"]      = "Adónde Van las Donaciones",
            ["trans.noAllocation"]   = "Todavía no hay datos de asignación disponibles.",
            ["trans.casesSuffix"]    = "casos",
            ["trans.monthlyActivity"] = "Actividad Mensual (Últimos 6 Meses)",
            ["trans.colMonth"]       = "Mes",
            ["trans.colDonations"]   = "Donaciones Recibidas",
            ["trans.colFamilies"]    = "Familias Atendidas",
            ["trans.colNewRequests"] = "Solicitudes Nuevas",
            ["trans.missionTitle"]   = "Nuestra Misión",
            ["trans.missionBody"]    = "El Ministerio LOTV es un apostolado católico dedicado a servir a las familias necesitadas mediante la distribución coordinada de alimentos, ropa, asistencia de vivienda y otros recursos esenciales. Cada dólar donado se destina directamente a servir a nuestros vecinos.",
            ["trans.ctaDonateTitle"] = "Haga una Donación",
            ["trans.ctaDonateBody"]  = "Apoye a familias en su comunidad",
            ["trans.ctaVolunteerTitle"] = "Conviértase en Voluntario",
            ["trans.ctaVolunteerBody"]  = "Done su tiempo y talento",
            ["trans.ctaApplyTitle"]  = "Solicite Asistencia",
            ["trans.ctaApplyBody"]   = "Estamos aquí para ayudar",
            ["trans.footerNote"]     = "Datos actualizados en tiempo real. Todas las cifras son totales agregados — no se muestra información de identificación personal.",
            ["trans.staffLogin"]     = "Acceso del personal",
            // Events
            ["events.title"]         = "Próximos Eventos",
            ["events.past"]          = "Eventos Pasados",
            ["events.rsvp"]          = "Reservar / Comprar Boletos",
            ["events.heroBlurb"]     = "Únase a nosotros en galas, subastas silenciosas y reuniones comunitarias. Cada evento apoya a familias necesitadas.",
            ["events.loading"]       = "Cargando eventos…",
            ["events.emptyTitle"]    = "No Hay Próximos Eventos",
            ["events.emptyBody"]     = "Vuelva pronto — se están planeando eventos.",
            ["events.virtual"]       = "Virtual",
            ["events.inPerson"]      = "Presencial",
            ["events.date"]          = "Fecha",
            ["events.location"]      = "Ubicación",
            ["events.online"]        = "En línea",
            ["events.tbd"]           = "Por confirmar",
            ["events.registered"]    = "inscritos",
            ["events.spotsTotal"]    = "cupos totales",
            ["events.full"]          = "Este evento está lleno",
            ["events.almostFull"]    = "⚠ Casi lleno — quedan {0} cupos",
            ["events.eventFull"]     = "Evento Lleno",
            ["events.rsvpRegister"]  = "Reservar / Inscribirse",
            ["events.attended"]      = "asistieron",
            ["events.modalYourName"] = "Su Nombre",
            ["events.modalEmail"]    = "Correo Electrónico",
            ["events.modalGuests"]   = "Número de invitados",
            ["events.modalConfirm"]  = "Confirmar Inscripción",
            ["events.modalClose"]    = "Cerrar",
            ["events.modalConfirmed"] = "✓ ¡Está inscrito! Se enviará una confirmación a {0}.",
            ["events.errName"]       = "El nombre es obligatorio.",
            ["events.errEmail"]      = "El correo electrónico es obligatorio.",
            ["events.errSubmit"]     = "No se pudo completar la inscripción. Por favor inténtelo de nuevo.",
            ["common.optional"]      = "(opcional)",
        },
    };
}
