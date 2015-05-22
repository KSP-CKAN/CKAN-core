#define NEW_COMPARE

using System;
using System.Text.RegularExpressions;
using Newtonsoft.Json;

namespace CKAN {
    /// <summary>
    /// Version comparison utilities.
    /// </summary>

    [Serializable]
    [JsonConverter(typeof(JsonSimpleStringConverter))]
    public class Version : IComparable<Version> {
        private bool _valid = false;
        private readonly int epoch = 0;
        private readonly string version;
        private readonly string orig_string;

        internal readonly string[] _fragments;

        // static readonly ILog log = LogManager.GetLogger(typeof(RegistryManager));
        public const string AutodetectedDllString = "autodetected dll";

        private const string VERSION_REG_EX_STRING = @"^[vV]?((?:<epoch>[\d]+)[:])?(?:<version>[\d]+(\.?[\w]+)*)$";

        public int EpochPart
        {
            get { return epoch; }
        }

        public string VersionPart
        {
            get { return version; }
        }

        public bool Valid { get{ return _valid; } }

        /// <summary>
        /// Creates a new version object from the `ToString()` representation of anything!
        /// </summary>
        public Version (string version) {
            orig_string = version;

            // determine if we're looking at a wellformatted version string
            Match match = Regex.Match(version, VERSION_REG_EX_STRING, RegexOptions.ECMAScript);
            if (match.Success)
            {
                // checks if there was an epoch, and saves it
                if (match.Groups["epoch"].Value.Length > 0)
                {
                    epoch = Convert.ToInt32(match.Groups["epoch"].Value);
                }

                version = match.Groups["version"].Value;

                // goes with the assumption that most versions are of type #.#.#.#
                _fragments = version.Contains(".")
                        ? version.Split('.')
                        : new string[] { version }
                    ;

                _valid = true;

            }
            // catch the bad case
            // it's not well formatted, so epoch must be 0, and version is whatever the original string is
            if(!_valid)
            {
                this.version = orig_string;
            }
        }

        override public string ToString() {
            return orig_string;
        }

        // When cast from a string.
        public static explicit operator Version(string v) {
            return new Version (v);
        }

        /// <summary>
        /// Returns -1 if this is less than that
        /// Returns +1 if this is greater than that
        /// Returns  0 if equal.
        /// </summary>
        public int CompareTo(Version that)
        {
            // check for invalid strings
            if (Valid)
            {
                if (that.Valid)
                {
                    // sanity check, see if both are equal
                    if (that.epoch == epoch && that.version == version)
                    {
                        return 0;
                    }

                    // Compare epochs first.
                    if (epoch < that.epoch)
                    {
                        return -1;
                    }
                    else if (epoch > that.epoch)
                    {
                        return 1;
                    }

                    // epocs the same, so examine the version number

                    // catches the 1.2.3 and 1.2 comparison differences
                    int maxSize =
                        _fragments.Length < that._fragments.Length
                                ? _fragments.Length
                                : that._fragments.Length
                        ;

                    int retVal;
                    int thisFragInt, thatFragInt;

                    // ingore first since that's the epoch
                    for (int x = 0; x < maxSize; x++)
                    {
                        // attempt int conversion
                        if (int.TryParse(_fragments[x], out thisFragInt) && int.TryParse(that._fragments[x], out thatFragInt))
                        {
                            // check if equal, if not return
                            if (thisFragInt > thatFragInt) { return 1; }
                            else if (thisFragInt < thatFragInt) { return -1; }
                        }
                        else
                        {
                            // not ints, toss exception and default to string compare
                            retVal = String.Compare(_fragments[x], that._fragments[x]);
                            if (retVal != 0) { return retVal; }
                        }
                    }

                    // whover's got the most segments wins
                    if (_fragments.Length < that._fragments.Length) { return -1; }
                    else if (_fragments.Length > that._fragments.Length) { return 1; }

                    // if we're here, they must be equal
                    else { return 0; }
                }
                // this is valid, that's not, so this one wins.
                else { return 1; }
            }
            else if(that.Valid)
            {
                // that's valid, so that wins
                return -1;
            }
            // defaults to string compare if both aren't valid
            else { return String.Compare(version, that.version); }
        }

        public bool IsEqualTo(Version that) {
            return CompareTo (that) == 0;
        }

        public bool IsLessThan(Version that) {
            return CompareTo (that) < 0;
        }

        public bool IsGreaterThan(Version that) {
            return CompareTo (that) > 0;
        }

        public override bool Equals(object obj)
        {
            var other = obj as Version;
            return other != null ? IsEqualTo(other) : base.Equals(obj);
        }
        public override int GetHashCode()
        {
            return version.GetHashCode();
        }
        int IComparable<Version>.CompareTo(Version other)
        {
            return CompareTo(other);
        }

        public static bool operator <(Version v1, Version v2)
        {
            return v1.CompareTo(v2) < 0;
        }

        public static bool operator <=(Version v1, Version v2)
        {
            return v1.CompareTo(v2) <= 0;
        }

        public static bool operator >(Version v1, Version v2)
        {
            return v1.CompareTo(v2) > 0;
        }

        public static bool operator >=(Version v1, Version v2)
        {
            return v1.CompareTo(v2) >= 0;
        }
    }

    /// <summary>
    /// This class represents a DllVersion. They don't have real
    /// version numbers or anything
    /// </summary>
    public class DllVersion : Version {
        public DllVersion() :base("0")
        {
        }

        override public string ToString()
        {            
            return AutodetectedDllString;
        }
    }

    /// <summary>
    /// This class represents a virtual version that was provided by
    /// another module.
    /// </summary>
    public class ProvidesVersion : Version {
        internal readonly string provided_by;

        public ProvidesVersion(string provided_by) :base("0")
        {
            this.provided_by = provided_by;
        }

        override public string ToString()
        {
            return string.Format("provided by {0}", provided_by);
        }
    }
}
