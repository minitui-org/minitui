namespace MiniTui;

/// <summary>Key routing (§9.4), hints and help (§9.5) for screens and overlays.</summary>
internal static class KeyRouter
{
    // A focus cycle is a bug in the app; don't loop forever on one
    private const int MaxChainLength = 64;

    /// <summary>
    /// The focus chain: the top overlay (or, with none open, the top screen), then each component's focused child.
    /// </summary>
    public static List<Component> Chain(App app)
    {
        var chain = new List<Component>();
        Component? root = app.Overlays.Top is { } overlay ? overlay : app.Screens.Top;
        for (var c = root; c is not null && chain.Count < MaxChainLength; c = c.FocusScope.Focused)
            chain.Add(c);
        return chain;
    }

    /// <summary>Steps 2–5 of §9.4. Returns true if the key was handled (or swallowed by an overlay).</summary>
    public static bool Route(App app, ConsoleKeyInfo key)
    {
        // 2. Overlays are modal: the top one gets the key, and routing always stops here
        if (app.Overlays.Top is { } overlay)
        {
            if (!RouteChain(Chain(app), key) && key.Key == ConsoleKey.Escape && overlay.CloseOnEscape)
                app.Overlays.Close(overlay);
            return true;
        }

        if (app.Screens.Top is not { } screen) return false;

        // 3. The focus chain, innermost first
        var chain = Chain(app);
        if (RouteChain(chain, key)) return true;

        // 4. Global bindings, then the menu bar's Alt+mnemonics (1.x spec §15.5)
        if (app.Keys.Handle(key, skipPlainCharacters: chain[^1].CapturesText)) return true;
        if (app.MenuBar?.HandleKey(key) == true) return true;

        // 5. Esc pops the screen
        if (key.Key == ConsoleKey.Escape && screen.PopOnEscape && app.Screens.Count > 1)
        {
            app.Screens.Pop();
            return true;
        }
        return false;
    }

    /// <summary>
    /// A paste goes down the focus chain, innermost first, to the top overlay's chain while one is open. Key maps
    /// aren't consulted (1.x spec §3.4). Returns true if a component handled it.
    /// </summary>
    public static bool RoutePaste(App app, string text)
    {
        if (app.Overlays.Top is null && app.Screens.Top is null) return false;
        var chain = Chain(app);
        for (var i = chain.Count - 1; i >= 0; i--)
            if (chain[i].HandlePaste(text)) return true;
        return false;
    }

    private static bool RouteChain(List<Component> chain, ConsoleKeyInfo key)
    {
        var capture = chain[^1].CapturesText;
        for (var i = chain.Count - 1; i >= 0; i--)
        {
            var component = chain[i];
            if (component.HandleKey(key)) return true;
            if (component.Keys.Handle(key, skipPlainCharacters: capture && i < chain.Count - 1)) return true;
        }

        // Unhandled Tab cycles the innermost scope with at least two members
        // Ctrl+Tab and Alt+Tab aren't Tab (the keyboard protocols report them, 1.x spec §12.5)
        if (key.Key != ConsoleKey.Tab || (key.Modifiers & (ConsoleModifiers.Control | ConsoleModifiers.Alt)) != 0) return false;
        for (var i = chain.Count - 1; i >= 0; i--)
        {
            var scope = chain[i].FocusScope;
            if (scope.Count < 2) continue;
            if ((key.Modifiers & ConsoleModifiers.Shift) != 0) scope.Previous();
            else scope.Next();
            return true;
        }
        return false;
    }

    /// <summary>
    /// The key maps in scope, innermost first: the focus chain's, then <see cref="App.Keys"/> unless an overlay
    /// is open (overlays swallow every key, so the global bindings can't fire).
    /// </summary>
    private static List<(string Group, KeyMap Keys)> Scopes(App app)
    {
        var chain = Chain(app);
        var scopes = new List<(string, KeyMap)>();
        for (var i = chain.Count - 1; i >= 0; i--) scopes.Add((chain[i].KeysGroup, chain[i].Keys));
        if (!app.Overlays.Any) scopes.Add(("Global", app.Keys));
        return scopes;
    }

    /// <summary>For each scope (innermost first), its entries with the labels of the bindings no inner scope shadows.</summary>
    internal static List<(string Group, List<(KeyMap.Entry Entry, string Keys)> Entries)> Visible(App app)
    {
        var seen = new HashSet<KeyBinding>();
        var result = new List<(string, List<(KeyMap.Entry, string)>)>();
        foreach (var (group, keys) in Scopes(app))
        {
            var entries = new List<(KeyMap.Entry, string)>();
            var shadowed = new HashSet<KeyBinding>(seen);
            foreach (var entry in keys.Entries)
            {
                var labels = new List<string>();
                for (var b = 0; b < entry.Bindings.Length; b++)
                {
                    if (!shadowed.Contains(entry.Bindings[b])) labels.Add(entry.Labels[b]);
                    shadowed.Add(entry.Bindings[b]);
                    seen.Add(entry.Bindings[b]);
                }
                if (labels.Count > 0) entries.Add((entry, string.Join(", ", labels)));
            }
            result.Add((group, entries));
        }
        return result;
    }

    /// <summary>
    /// Hints for the bindings in scope that show in hints, innermost first, each key once. Each shows its first key
    /// that can fire now: while the focus captures text, an outer binding's plain characters can't (1.x spec §26.3).
    /// </summary>
    public static string Hints(App app, int maxWidth)
    {
        var hints = new List<string>();
        var visible = Visible(app);
        var capture = Chain(app) is [.., var focused] && focused.CapturesText;
        var escBound = visible.Any(g => g.Entries.Any(e => e.Entry.Bindings.Contains(Escape)));
        // Without an overlay, the last group is Global
        var global = app.Overlays.Any ? -1 : visible.Count - 1;
        for (var g = 0; g < visible.Count; g++)
        {
            // 1.x spec §22.3: on a pushed screen, Esc goes back, after the screen's own hints
            if (g == global && !escBound && app.Screens is { Count: > 1, Top.PopOnEscape: true }) hints.Add("esc Back");
            foreach (var (entry, keys) in visible[g].Entries)
            {
                if (!entry.ShowInHints) continue;
                // Group 0 is the focused component, whose own plain-character bindings still fire
                var key = keys.Split(", ").FirstOrDefault(label =>
                    g == 0 || !capture || !entry.Bindings[Array.IndexOf(entry.Labels, label)].IsPlainCharacter);
                if (key is null) continue;
                var hint = $"{key} {entry.Description}";
                if (!hints.Contains(hint)) hints.Add(hint);
            }
        }
        return KeyMap.FitHints(hints, maxWidth);
    }

    private static readonly KeyBinding Escape = KeyBinding.Parse("esc");

    /// <summary>
    /// Help text for the screens' focus chain: a group per component under its <see cref="Component.KeysGroup"/>,
    /// outermost first, then "Global". Shadowed bindings are left out.
    /// </summary>
    public static List<string> HelpLines(App app)
    {
        // Visible is innermost first with Global last; help lists the chain outermost first, then Global
        var components = Visible(app);
        var hasGlobal = !app.Overlays.Any;
        components.Reverse(0, components.Count - (hasGlobal ? 1 : 0));

        var width = 0;
        foreach (var (_, entries) in components)
            foreach (var (_, keys) in entries) width = Math.Max(width, UnicodeWidth.Of(keys));

        var lines = new List<string>();
        foreach (var (group, entries) in components)
        {
            if (entries.Count == 0) continue;
            if (lines.Count > 0) lines.Add("");
            lines.Add(group);
            foreach (var (entry, keys) in entries)
                lines.Add($"  {keys}{new string(' ', width - UnicodeWidth.Of(keys))}  {entry.Description}");
        }
        return lines;
    }
}
