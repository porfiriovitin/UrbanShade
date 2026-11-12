namespace UrbanShade.Domain.Entities;

public class City : EntityBase
{
    private static readonly Dictionary<string, string> StateCodes = new()
    {
        ["RO"] = "11",
        ["AC"] = "12",
        ["AM"] = "13",
        ["RR"] = "14",
        ["PA"] = "15",
        ["AP"] = "16",
        ["TO"] = "17",
        ["MA"] = "21",
        ["PI"] = "22",
        ["CE"] = "23",
        ["RN"] = "24",
        ["PB"] = "25",
        ["PE"] = "26",
        ["AL"] = "27",
        ["SE"] = "28",
        ["BA"] = "29",
        ["MG"] = "31",
        ["ES"] = "32",
        ["RJ"] = "33",
        ["SP"] = "35",
        ["PR"] = "41",
        ["SC"] = "42",
        ["RS"] = "43",
        ["MS"] = "50",
        ["MT"] = "51",
        ["GO"] = "52",
        ["DF"] = "53"
    };

    private City()
    {
    }

    public City(string name, string state, string ibgeCode)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 150)
            throw new ArgumentException("A city name with at most 150 characters is required.", nameof(name));
        if (!IsValidIbgeCode(state, ibgeCode))
            throw new ArgumentException("A valid state and matching seven-digit IBGE code are required.", nameof(ibgeCode));

        Name = name.Trim();
        State = state.Trim().ToUpperInvariant();
        IbgeCode = ibgeCode.Trim();
    }

    public string Name { get; private set; } = string.Empty;
    public string State { get; private set; } = string.Empty;
    public string IbgeCode { get; private set; } = string.Empty;

    public static bool IsValidIbgeCode(string? state, string? ibgeCode)
    {
        if (state is null || ibgeCode is null) return false;

        var code = ibgeCode.Trim();

        /// :: IBGE codes use seven digits; the first two identify the state.
        return code.Length == 7 && code.All(character => character >= '0' && character <= '9') && StateCodes.TryGetValue(state.Trim().ToUpperInvariant(), out var prefix) && code.StartsWith(prefix, StringComparison.Ordinal);
    }
}
