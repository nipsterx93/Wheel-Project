// -------------------------------------------------------------------------

// FILE: TyreManager.cs

// VERSION: Fix errori 9

// -------------------------------------------------------------------------

using System;

using System.Globalization;



namespace SimRIG

{

    // Enum per definire quali gomme cambiare

    public enum TyreSelectionScope

    {

        None,

        All4,

        Fronts,

        Rears,

        Left,

        Right,

        FL,

        FR,

        RL,

        RR

    }



    /// <summary>

    /// Si occupa esclusivamente di memorizzare la selezione delle gomme e delle pressioni,

    /// e di formattare i comandi testuali da inviare alla chat di iRacing.

    /// Non fa calcoli sui tempi dei pit stop.

    /// </summary>

    public class TyreManager

    {

        public TyreSelectionScope CurrentScope { get; private set; } = TyreSelectionScope.None;

        public int PressureClicks { get; private set; } = 0;

        public double UserPressureOffset => CalculateOffset();

        public int PressureOffsetKpa => (int)Math.Round(UserPressureOffset);

        public bool SelectedWetCompound { get; set; } = false;

        public string CurrentUnit { get; set; } = "bar";



        public TyreManager() { }



        // -------------------------------------------------------------------------

        // AZIONI UTENTE (Chiamate dagli input del volante)

        // -------------------------------------------------------------------------



        public void CycleTyreScope(int direction)

        {

            int current = (int)CurrentScope;

            int max = Enum.GetNames(typeof(TyreSelectionScope)).Length;



            current += direction;

            if (current < 0) current = max - 1;

            else if (current >= max) current = 0;



            CurrentScope = (TyreSelectionScope)current;

        }



        public void AdjustPressure(int clicks, string unit = "bar")

        {

            SetUnit(unit);

            PressureClicks += clicks;

        }



        public void SetUnit(string unit)

        {

            if (!string.IsNullOrWhiteSpace(unit))

            {

                CurrentUnit = unit.Trim().ToLowerInvariant();

            }

        }



        private double CalculateOffset()

        {

            string lower = (CurrentUnit ?? "bar").ToLowerInvariant();

            double step = 0.05;

            if (lower == "psi") step = 0.5;

            else if (lower == "kpa" || lower == "hpa") step = 5.0;



            double val = PressureClicks * step;

            if (lower == "bar") return Math.Round(val, 2);

            if (lower == "psi") return Math.Round(val, 1);

            return Math.Round(val, 1);

        }



        public void ToggleCompound()

        {

            SelectedWetCompound = !SelectedWetCompound;

        }



        public void ResetAll()

        {

            CurrentScope = TyreSelectionScope.None;

            PressureClicks = 0;

            SelectedWetCompound = false;

        }



        public void ResetPressureOnly()

        {

            PressureClicks = 0;

        }



        public void ClearTyresOnly()

        {

            CurrentScope = TyreSelectionScope.None;

        }



        // -------------------------------------------------------------------------

        // GENERAZIONE COMANDI CHAT IRACING

        // -------------------------------------------------------------------------



        public string GetTyreCommandString()

        {

            switch (CurrentScope)

            {

                case TyreSelectionScope.Fronts: return "#lf #rf";

                case TyreSelectionScope.Rears: return "#lr #rr";

                case TyreSelectionScope.Left: return "#lf #lr";

                case TyreSelectionScope.Right: return "#rf #rr";

                case TyreSelectionScope.FL: return "#lf";

                case TyreSelectionScope.FR: return "#rf";

                case TyreSelectionScope.RL: return "#lr";

                case TyreSelectionScope.RR: return "#rr";

                case TyreSelectionScope.None: return "#clear tires";

                default: return "#lf #rf #lr #rr";

            }

        }



        public string GetPressureCommandString()

        {

            string lower = (CurrentUnit ?? "bar").ToLowerInvariant();

            string unitSuffix = lower == "psi" ? "psi" : (lower == "kpa" || lower == "hpa" ? "kpa" : "bar");

            string formatSpecifier = lower == "psi" ? "0.0" : (lower == "kpa" || lower == "hpa" ? "0" : "0.00");



            double offset = UserPressureOffset;

            string sign = offset >= 0 ? "+" : "";

            string valStr = $"{sign}{offset.ToString(formatSpecifier, CultureInfo.InvariantCulture)}{unitSuffix}";



            string cmdBase = "";



            switch (CurrentScope)

            {

                case TyreSelectionScope.Fronts: cmdBase = "#lf {0} #rf {0}"; break;

                case TyreSelectionScope.Rears: cmdBase = "#lr {0} #rr {0}"; break;

                case TyreSelectionScope.Left: cmdBase = "#lf {0} #lr {0}"; break;

                case TyreSelectionScope.Right: cmdBase = "#rf {0} #rr {0}"; break;

                case TyreSelectionScope.FL: cmdBase = "#lf {0}"; break;

                case TyreSelectionScope.FR: cmdBase = "#rf {0}"; break;

                case TyreSelectionScope.RL: cmdBase = "#lr {0}"; break;

                case TyreSelectionScope.RR: cmdBase = "#rr {0}"; break;

                default: cmdBase = "#lf {0} #rf {0} #lr {0} #rr {0}"; break;

            }



            return string.Format(CultureInfo.InvariantCulture, cmdBase, valStr);

        }



        // -------------------------------------------------------------------------

        // UTILITY PER LA UI

        // -------------------------------------------------------------------------



        public string GetScopeLabel()

        {

            return CurrentScope.ToString().ToUpper().Replace("ALL4", "ALL 4");

        }



        public string GetPressureLabel()

        {

            string lower = (CurrentUnit ?? "bar").ToLowerInvariant();

            string displayUnit = lower == "psi" ? "psi" : (lower == "kpa" || lower == "hpa" ? "kPa" : "bar");

            string formatSpecifier = lower == "psi" ? "0.0" : (lower == "kpa" || lower == "hpa" ? "0" : "0.00");

            double offset = UserPressureOffset;

            string sign = offset >= 0 ? "+" : "";

            return $"{sign}{offset.ToString(formatSpecifier, CultureInfo.InvariantCulture)} {displayUnit}";

        }



        public double GetSelectedTireTime(string carClassId = "DEFAULT")

        {

            CarPitProfile profile = CarPitData.GetProfile(carClassId);

            switch (CurrentScope)

            {

                case TyreSelectionScope.All4:

                    return profile.Tires4;

                case TyreSelectionScope.Fronts:

                case TyreSelectionScope.Rears:

                case TyreSelectionScope.Left:

                case TyreSelectionScope.Right:

                    return profile.Tires2;

                case TyreSelectionScope.FL:

                case TyreSelectionScope.FR:

                case TyreSelectionScope.RL:

                case TyreSelectionScope.RR:

                    return profile.Tires1;

                case TyreSelectionScope.None:

                default:

                    return 0.0;

            }

        }

    }

}