namespace NinoBind.Core.Configuration;

public static class ConfigurationValidator
{
    public static string? Validate(ConfigurationDocument? document)
    {
        if (document is null)
            return "O documento está vazio.";
        if (document.SchemaVersion != Schema.CurrentVersion)
            return $"A versão {document.SchemaVersion} do arquivo não é suportada.";
        if (document.Profiles.Count == 0)
            return "O arquivo não tem perfis.";
        if (document.Settings is null)
            return "O arquivo não tem configurações.";
        if (string.IsNullOrWhiteSpace(document.Settings.Language))
            return "O idioma está vazio.";

        var profileIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var profile in document.Profiles)
        {
            if (string.IsNullOrWhiteSpace(profile.Id) || string.IsNullOrWhiteSpace(profile.Name))
                return "Há um perfil sem identificador ou nome.";
            if (!profileIds.Add(profile.Id))
                return $"O perfil {profile.Id} está repetido.";
        }

        if (document.Profiles.GroupBy(profile => profile.Name.Trim(), StringComparer.CurrentCultureIgnoreCase).Any(group => group.Count() > 1))
            return "Há perfis com o mesmo nome.";

        if (!profileIds.Contains(document.ActiveProfileId))
            return "O perfil ativo não existe.";

        foreach (var binding in document.Bindings)
        {
            if (string.IsNullOrWhiteSpace(binding.Id))
                return "Há um binding sem identificador.";
            if (!profileIds.Contains(binding.ProfileId))
                return $"O binding {binding.Id} aponta para um perfil inexistente.";
            var bindingError = ValidateBinding(document, binding);
            if (bindingError is not null)
                return bindingError;
        }

        var macroIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var macro in document.Macros)
        {
            if (string.IsNullOrWhiteSpace(macro.Id) || string.IsNullOrWhiteSpace(macro.Name))
                return "Há uma macro sem identificador ou nome.";
            if (!macroIds.Add(macro.Id))
                return $"A macro {macro.Id} está repetida.";
            var macroError = Macros.MacroRunner.Validate(macro);
            if (macroError is not null)
                return macroError;
        }

        return null;
    }

    public static string? ValidateBinding(ConfigurationDocument document, BindingDocument binding)
    {
        if (binding.InputKind is not ("key" or "mouse"))
            return "A entrada do binding é inválida.";
        if (binding.InputKind == "key" && binding.InputCode is < 1 or > 254)
            return "A tecla de origem é inválida.";
        if (binding.InputKind == "key" && Bindings.BindingEngine.IsReservedKey(binding.InputCode))
            return binding.InputCode == Bindings.BindingEngine.ReservedScrollLockKey
                ? "Scroll Lock fica reservada para ligar e desligar o motor."
                : "Pause fica reservada para o Emergency Stop.";
        if (binding.InputKind == "mouse" && binding.InputCode is < 1 or > 5)
            return "O botão de origem é inválido.";

        switch (binding.ActionKind)
        {
            case "none":
            case "suppress":
                return null;
            case "key":
                return binding.OutputCode is < 1 or > 254 ? "A tecla de destino é inválida." : null;
            case "mouse":
                return binding.OutputCode is < 1 or > 5 ? "O botão de destino é inválido." : null;
            case "chord":
                if (binding.Chord.Count is < 1 or > 6)
                    return "A combinação precisa ter de 1 a 6 teclas.";
                return binding.Chord.Any(key => key is < 1 or > 254) ? "A combinação tem uma tecla inválida." : null;
            case "text":
                if (string.IsNullOrWhiteSpace(binding.Text) || binding.Text.Length > 256)
                    return "O texto precisa ter entre 1 e 256 caracteres.";
                return binding.Text.Any(character => char.IsControl(character))
                    ? "O texto não pode ter caracteres de controle."
                    : null;
            case "macro":
                return document.Macros.Any(macro => macro.Id == binding.MacroId)
                    ? null
                    : "A macro selecionada não existe.";
            default:
                return "O tipo de ação é inválido.";
        }
    }
}
