#define OLD_COMPARE
//#define NEW_COMPARE

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
        private bool _valid;
        private readonly int epoch;
        private readonly string version;
        private readonly string orig_string;
        // static readonly ILog log = LogManager.GetLogger(typeof(RegistryManager));
        public const string AutodetectedDllString = "autodetected dll";
#if(OLD_COMPARE)
        private const string VERSION_REG_EX_STRING = @"^[v]?([\d][:])?[\d]+(\.?[\da-zA-Z]+)*$";
#elif(NEW_COMPARE)
        private const string VERSION_REG_EX_STRING = @"^[v]?([\d][:])?[\d]+(\.?[\da-zA-Z]+)*$";
#endif

        public int EpochPart
        {
            get { return epoch; }
        }

        public string VersionPart
        {
            get { return version; }
        }

        public bool Valid { get{ return _valid; } }

#if (OLD_COMPARE)
        public struct Comparison {
            public int compare_to;
            public string remainder1;
            public string remainder2;
        }
#endif

        /// <summary>
        /// Creates a new version object from the `ToString()` representation of anything!
        /// </summary>
        public Version (string version) {
            orig_string = version;
            _valid = false; // bad until proven good, simplifies error trapping

            // determine if we're looking at a wellformatted version string
            // NOTE: processing is done with lower case to simplify processing
            string workingCopy = orig_string.ToLower();
            if (Regex.IsMatch(workingCopy, VERSION_REG_EX_STRING))
            {

                // trim leading 'v'
                if (workingCopy[0] == 'v') { workingCopy = workingCopy.Substring(1); }

                // check if it has an epoch
                if (version.Contains(":"))
                {
                    // does break into two pieces
                    string[] fragments = version.Split(':');

                    // should be exactly two fragments
                    if(fragments.Length == 2)
                    {
                        // first fragement should be an integer
                        if(int.TryParse(fragments[0], out epoch))
                        {
                            _valid = true;
#if (NEW_COMPARE)
                            this.version = fragments[1];
#endif
                        }
                    }
                }
                else
                {
                    _valid = true;
#if (NEW_COMPARE)
                    this.version = workingCopy;
#endif
                }
            }
#if (NEW_COMPARE)
            // catch the bad case
            // it's not well formatted, so epoch must be 0, and version is whatever the original string is
            if(!_valid)
            {
                this.version = orig_string;
                epoch = 0;

            }
#endif
#if (OLD_COMPARE)
            Match match = Regex.Match (
                version,
                @"^(?:(?<epoch>[0-9]+):)?(?<version>.*)$"
            );

            // If we have an epoch, then record it.
            if (match.Groups["epoch"].Value.Length > 0) {
                epoch = Convert.ToInt32( match.Groups["epoch"].Value );
            }

            this.version = match.Groups["version"].Value;
#endif
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
        public int CompareTo(Version that) {
#if(NEW_COMPARE)
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

                    // break apart string by "." into segments
                    string[] thisVersion =
                        version.Contains(".")
                            ? version.Split('.')
                            : new string[] { "" }
                        ;

                    string[] thatVersion =
                        that.version.Contains(".")
                            ? that.version.Split('.')
                            : new string[] { "" }
                        ;

                    // catches the 1.2.3 and 1.2 comparison differences
                    int maxSize =
                        thisVersion.Length < thatVersion.Length
                                ? thisVersion.Length
                                : thatVersion.Length
                                ;

                    int retVal;
                    int thisFragInt, thatFragInt;

                    // ingore first since that's the epoch
                    for (int x = 0; x < maxSize; x++)
                    {
                        // attempt int conversion
                        if (int.TryParse(thisVersion[x], out thisFragInt) && int.TryParse(thatVersion[x], out thatFragInt))
                        {
                            // check if equal, if not return
                            if (thisFragInt > thatFragInt) { return 1; }
                            else if (thisFragInt < thatFragInt) { return -1; }
                        }
                        else
                        {
                            // not ints, toss exception and default to string compare
                            retVal = String.Compare(thisVersion[x], thatVersion[x]);
                            if (retVal != 0) { return retVal; }
                        }
                    }

                    // whover's got the most segments wins
                    if (thisVersion.Length < thatVersion.Length) { return -1; }
                    else if (thisVersion.Length > thatVersion.Length) { return 1; }

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

#elif(OLD_COMPARE)

            if (that.epoch == epoch && that.version == version) {
                return 0;
            }
 
            // Compare epochs first.
            if (epoch != that.epoch) {
                return epoch > that.epoch ?1:-1;
            }                                  

            // Epochs are the same. Do the dance described in
            // https://github.com/KSP-CKAN/CKAN/blob/master/Spec.md#version-ordering

            Comparison comp;
            comp.remainder1 = version;
            comp.remainder2 = that.version;

            // Process our strings while there are characters remaining
            while (comp.remainder1.Length > 0 && comp.remainder2.Length > 0) {

                // Start by comparing the string parts.
                comp = StringComp (comp.remainder1, comp.remainder2);

                // If we've found a difference, return it.
                if (comp.compare_to != 0) {
                    return comp.compare_to;
                }

                // Otherwise, compare the number parts.
                // It's okay not to check if our strings are exhausted, because
                // if they are the exhausted parts will return zero.

                comp = NumComp (comp.remainder1, comp.remainder2);

                // Again, return difference if found.
                if (comp.compare_to != 0) {
                    return comp.compare_to;
                }
            }

            // Oh, we've run out of one or both strings.
            // They *can't* be equal, because we would have detected that in our first test.
            // So, whichever version is empty first is the smallest. (1.2 < 1.2.3)

            if (comp.remainder1.Length == 0) {
                return -1;
            }

            return 1;
#endif

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

#if (OLD_COMPARE)
        /// <summary>
        /// Compare the leading non-numerical parts of two strings
        /// </summary>
       
        internal static Comparison StringComp(string v1, string v2)
        {
            var comp = new Comparison {remainder1 = "", remainder2 = ""};

            // Our starting assumptions are that both versions are completely
            // strings, with no remainder. We'll then check if they're not.

            string str1 = v1;
            string str2 = v2;

            // Start by walking along our version string until we find a number,
            // thereby finding the starting string in both cases. If we fall off
            // the end, then our assumptions made above hold.

            for (int i = 0; i < v1.Length; i++)
            {
                if (Char.IsNumber(v1[i]))
                {
                    comp.remainder1 = v1.Substring(i);
                    str1 = v1.Substring(0, i);
                    break;
                }
            }

            for (int i = 0; i < v2.Length; i++)
            {
                if (Char.IsNumber(v2[i]))
                {
                    comp.remainder2 = v2.Substring(i);
                    str2 = v2.Substring(0, i);
                    break;
                }
            }

            // Then compare the two strings, and return our comparison state.

            comp.compare_to = String.CompareOrdinal(str1, str2);
            return comp;
        }

        /// <summary>
        /// Compare the leading numerical parts of two strings
        /// </summary>

        internal static Comparison NumComp(string v1, string v2)
        {
            var comp = new Comparison {remainder1 = "", remainder2 = ""};

            int minimum_length1 = 0;
            for (int i = 0; i < v1.Length; i++)
            {
                if (!char.IsNumber(v1[i]))
                {
                    comp.remainder1 = v1.Substring(i);
                    break;
                }

                minimum_length1++;
            }

            int minimum_length2 = 0;
            for (int i = 0; i < v2.Length; i++)
            {
                if (!char.IsNumber(v2[i]))
                {
                    comp.remainder2 = v2.Substring(i);
                    break;
                }

                minimum_length2++;
            }

            int integer1;
            int integer2;

            if (!int.TryParse(v1.Substring(0, minimum_length1), out integer1))
            {
                integer1 = 0;
            }

            if (!int.TryParse(v2.Substring(0, minimum_length2), out integer2))
            {
                integer2 = 0;
            }

            comp.compare_to = integer1.CompareTo(integer2);
            return comp;
        }
#endif

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

