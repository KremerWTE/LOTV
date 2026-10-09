using Lotv.Core.Models;

namespace Lotv.Api.Data;

/// <summary>
/// Brings any prayer-care intake definition up to date WITHOUT touching the database: it is applied every time the form is
/// served or opened in the editor, so a form staff saved long ago still gets
///   • the "Child's name" question (shown only for a loss), and
///   • Spanish text for every built-in question, option and message that has no Spanish yet.
/// Spanish that staff wrote in the editor always wins; only blanks are filled. Saving the form in the editor stores the result.
/// </summary>
public static class FormUpgrades
{
    public const string ChildNameKey = "childName";
    public static readonly string[] LossReasons = ["Miscarriage", "Stillbirth", "InfantLoss", "PastLoss"];

    /// <summary>The "Child's name" answer out of a family's contact notes ("Quarterly Grief Support requested: Yes | Child's name: Grace | ..."), or null.</summary>
    public static string? ChildNameFrom(string? contactNotes)
    {
        if (string.IsNullOrWhiteSpace(contactNotes)) return null;
        var m = System.Text.RegularExpressions.Regex.Match(contactNotes, @"(?:^|\|)\s*Child's name:\s*([^|]+)");
        var name = m.Success ? m.Groups[1].Value.Trim() : "";
        return name.Length is > 0 and <= 120 ? name : null;
    }

    public static IntakeFormDefinition Apply(IntakeFormDefinition def)
    {
        EnsureChildName(def);
        FillSpanish(def);
        return def;
    }

    private static void EnsureChildName(IntakeFormDefinition def)
    {
        if (def.Fields.Any(f => f.Key == ChildNameKey)) return;
        var id = def.Fields.Any(f => f.Id == "child-name") ? "child-name-" + Guid.NewGuid().ToString("N")[..6] : "child-name";
        var field = new IntakeFormField
        {
            Id = id, Key = ChildNameKey, Type = "text",
            Label = "Child's name",
            Help = "Optional. If you'd like to share it, we will pray for your baby by name.",
            Width = "full", Visible = true, Required = false, Standard = false,
            ShowWhen = [new FormCondition { Field = "reason", In = [.. LossReasons] }],
        };
        var at = def.Fields.FindIndex(f => f.Key == "dateOfLoss");
        if (at < 0) at = def.Fields.FindIndex(f => f.Key == "reason");
        def.Fields.Insert(at < 0 ? def.Fields.Count : at + 1, field);
    }

    private static Dictionary<string, string> Fill(Dictionary<string, string>? es, params (string Key, string Text)[] texts)
    {
        es ??= new Dictionary<string, string>();
        foreach (var (k, v) in texts)
            if (!es.TryGetValue(k, out var cur) || string.IsNullOrWhiteSpace(cur)) es[k] = v;
        return es;
    }

    private static void FillSpanish(IntakeFormDefinition def)
    {
        def.Es = Fill(def.Es,
            ("title", "Solicite un Paquete de Cuidado en Oración"),
            ("intro", "Para solicitar que se envíe por correo un Paquete de Cuidado en Oración a usted o a una familia que necesita apoyo, complete el formulario a continuación."),
            ("submitLabel", "Enviar Solicitud"),
            ("footerNote", "Su información se mantiene estrictamente confidencial y se utiliza únicamente para atender su solicitud. Nunca hay ningún costo ni obligación."),
            ("toggleLabelMe", "Es para mí"),
            ("toggleLabelSomeone", "Es para otra persona"),
            ("whoIsThisForLabel", "¿Para quién es esto?"));
        def.Confirmation.Es = Fill(def.Confirmation.Es,
            ("title", "Hemos recibido su solicitud."),
            ("body", "Gracias por comunicarse con nosotros. Un miembro de nuestro equipo revisará su solicitud y se pondrá en contacto con usted pronto. Sepa que usted y su familia están presentes en nuestras oraciones."));
        def.Donation.Es = Fill(def.Donation.Es,
            ("title", "Ayúdenos a Continuar Este Ministerio"),
            ("body", "Recibir un Paquete de Cuidado en Oración no tiene ningún costo. Si desea ayudarnos a enviar paquetes a otras familias, puede hacer una donación opcional a continuación. Una donación nunca es obligatoria."));

        foreach (var f in def.Fields)
        {
            if (string.IsNullOrEmpty(f.Key))
            {
                // headings and hints have no key: match them by their English text
                if (Headings.TryGetValue(f.Label.Trim(), out var h)) f.Es = Fill(f.Es, h);
                continue;
            }
            if (FieldText.TryGetValue(f.Key, out var texts)) f.Es = Fill(f.Es, texts);
            foreach (var o in f.Options)
                if (OptionText.TryGetValue((f.Key, o.Value), out var ot)) o.Es = Fill(o.Es, ot);
        }
    }

    private static readonly Dictionary<string, (string, string)[]> Headings = new()
    {
        ["About You"] = [("label", "Acerca de usted"), ("labelMe", "Acerca de usted"), ("labelSomeone", "Acerca del destinatario")],
        ["About You (the person referring this family)"] = [("label", "Acerca de usted (la persona que refiere a esta familia)")],
        ["Opt-in Communications"] = [("label", "Comunicaciones opcionales")],
        ["Phone numbers are optional."] = [("label", "Los números de teléfono son opcionales.")],
    };

    private const string BraceletMe =
        "Niños para la pulsera: Nos gustaría incluir una pulsera personalizada en su Paquete de Cuidado en Oración. Por favor comparta las iniciales de todos sus hijos en orden de nacimiento, incluidos los que están en el cielo. Si su hijo no recibió un nombre o si usted está experimentando infertilidad, colocaremos cuentas especiales de Corazón en su pulsera.";
    private const string BraceletSomeone =
        "Niños para la pulsera: Nos gustaría incluir una pulsera personalizada en el Paquete de Cuidado en Oración de su destinatario. Por favor comparta las iniciales de todos los hijos de su destinatario en orden de nacimiento, incluidos los que están en el cielo. Si el hijo de su destinatario no recibió un nombre o si está experimentando infertilidad, colocaremos cuentas especiales de Corazón en su pulsera.";
    private const string StoryMe = "Por favor comparta con nosotros, en la medida en que se sienta cómodo/a, su historia:";
    private const string StorySomeone = "Por favor comparta con nosotros, en la medida en que se sienta cómodo/a, la historia de su destinatario:";

    private static readonly Dictionary<string, (string, string)[]> FieldText = new()
    {
        ["wantsPackage"] = [("label", "¿Qué le ayudaría más en este momento?")],
        ["husbandFirst"] = [("label", "Nombre del esposo"), ("placeholder", "Nombre y apellido")],
        ["wifeFirst"] = [("label", "Nombre de la esposa"), ("placeholder", "Nombre y apellido")],
        ["husbandEmail"] = [("label", "Correo electrónico del esposo")],
        ["wifeEmail"] = [("label", "Correo electrónico de la esposa")],
        ["husbandPhone"] = [("label", "Teléfono del esposo")],
        ["wifePhone"] = [("label", "Teléfono de la esposa")],
        ["street"] = [("label", "Dirección")],
        ["apt"] = [("label", "Apartamento o Suite #")],
        ["city"] = [("label", "Ciudad")],
        ["state"] = [("label", "Estado")],
        ["zip"] = [("label", "Código postal")],
        ["mentionPreference"] =
        [
            ("label", "¿Desea que mencionemos que este paquete es de parte suya, o prefiere permanecer en el anonimato?"),
            ("labelPrayerOnly", "¿Desea que mencionemos que esta petición de oración es de parte suya, o prefiere permanecer en el anonimato?"),
        ],
        ["reason"] = [("label", "Motivo de la Petición de Oración")],
        ["dateOfLoss"] = [("label", "Fecha de la pérdida reciente")],
        [ChildNameKey] = [("label", "Nombre del niño"), ("help", "Opcional. Si desea compartirlo, rezaremos por su bebé por su nombre.")],
        ["griefSupport"] = [("label", "¿Le gustaría recibir apoyo trimestral para el duelo?")],
        ["faithTradition"] = [("label", "Tradición de fe")],
        ["diocese"] = [("label", "Diócesis")],
        ["parish"] = [("label", "Parroquia")],
        ["howHeard"] = [("label", "¿Cómo se enteró de nosotros?")],
        ["howHeardOther"] = [("label", "Cuéntenos más"), ("placeholder", "Por favor, cuéntenos más…")],
        ["customMessage"] = [("label", "Incluya un mensaje personalizado para su destinatario")],
        ["story"] = [("label", StoryMe), ("labelMe", StoryMe), ("labelSomeone", StorySomeone)],
        ["childrenInitials"] =
        [
            ("label", BraceletMe), ("labelMe", BraceletMe), ("labelSomeone", BraceletSomeone),
            ("buttonLabel", "+ Agregar Otro Niño"), ("buttonLabelSomeone", "+ Agregar Nuevo"),
        ],
        ["requesterFirst"] = [("label", "Su nombre")],
        ["requesterLast"] = [("label", "Su apellido")],
        ["requesterEmail"] = [("label", "Su correo electrónico")],
        ["requesterPhone"] = [("label", "Su teléfono")],
        ["optinNewsletter"] = [("label", "Suscríbase a nuestro boletín: noticias mensuales y enlaces a eventos")],
        ["optinPrayerNight"] = [("label", "Envíenme invitaciones a la Noche de Oración: nuestra Noche de Oración mensual por Zoom")],
    };

    private static readonly Dictionary<(string, string), (string, string)[]> OptionText = new()
    {
        [("wantsPackage", "Package")] = [("label", "Un paquete de consuelo enviado a usted, además de oración continua"), ("labelSomeone", "Un paquete de consuelo enviado a la familia, además de oración continua")],
        [("wantsPackage", "PrayerOnly")] = [("label", "Solo oración — no se necesita paquete")],
        [("wantsPackage", "PackageOnly")] = [("label", "Solo un paquete de consuelo — no se necesita equipo de oración"), ("labelSomeone", "Solo un paquete de consuelo para la familia — no se necesita equipo de oración")],
        [("mentionPreference", "Mention")] = [("label", "Mencionarme")],
        [("mentionPreference", "Anonymous")] = [("label", "Permanecer anónimo")],
        [("reason", "Infertility")] = [("label", "Infertilidad")],
        [("reason", "PrenatalDiagnosis")] = [("label", "Diagnóstico prenatal")],
        [("reason", "PrenatalLifeLimitingDiagnosis")] = [("label", "Diagnóstico prenatal que limita la vida")],
        [("reason", "Miscarriage")] = [("label", "Aborto espontáneo (antes de las 20 semanas)")],
        [("reason", "Stillbirth")] = [("label", "Muerte fetal (20 semanas o más)")],
        [("reason", "InfantLoss")] = [("label", "Pérdida infantil (después del nacimiento, hasta 1 año de edad)")],
        [("reason", "PastLoss")] = [("label", "Pérdida en el pasado")],
        [("reason", "PostnatalMedical")] = [("label", "Preocupación médica posnatal")],
        [("reason", "Other")] = [("label", "Otro")],
        [("griefSupport", "Yes")] = [("label", "Sí")],
        [("griefSupport", "No")] = [("label", "No")],
        [("faithTradition", "Catholic")] = [("label", "Católica")],
        [("faithTradition", "Christian")] = [("label", "Cristiana")],
        [("faithTradition", "Jewish")] = [("label", "Judía")],
        [("faithTradition", "Muslim")] = [("label", "Musulmana")],
        [("faithTradition", "Others")] = [("label", "Otra")],
        [("faithTradition", "Prefer Not to Say")] = [("label", "Prefiero no decirlo")],
        [("howHeard", "Friend")] = [("label", "Amigo/a")],
        [("howHeard", "Family")] = [("label", "Familiar")],
        [("howHeard", "Instagram")] = [("label", "Instagram")],
        [("howHeard", "Facebook")] = [("label", "Facebook")],
        [("howHeard", "Google Search")] = [("label", "Búsqueda en Google")],
        [("howHeard", "Medical Provider")] = [("label", "Proveedor médico")],
        [("howHeard", "Parish Website")] = [("label", "Sitio web de la parroquia")],
        [("howHeard", "Diocese's Website")] = [("label", "Sitio web de la diócesis")],
        [("howHeard", "Lily of the Valley Event")] = [("label", "Evento de Lily of the Valley")],
        [("howHeard", "Other")] = [("label", "Otro")],
    };
}
