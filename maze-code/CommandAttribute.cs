// |||| NAVIGATION - Custom attribute for commands for better code structure ||||
using System.Reflection;

namespace EnumCommand
{
    static class Extensions
    {
        /// <summary>
        /// Will get the command for a certain enum value that has been assigned the command attribute
        /// </summary>
        public static string GetCommand(this Enum value)
        {
            // Get the type
            Type type = value.GetType();

            // Get fieldinfo for this type
            FieldInfo? fieldInfo = type.GetField(value.ToString());

            // Get the attributes
            CommandAttribute[]? attribs = null;
            if (fieldInfo != null)
                attribs = fieldInfo.GetCustomAttributes(typeof(CommandAttribute), false) as CommandAttribute[];

            // Return the first match if there was one
            if (attribs != null)
                return attribs[0].CommandValue;
            else
                return "";
        }

        /// <summary>
        /// Will get the command for a certain enum value that has been assigned the command attribute, and add the parameters
        /// </summary>
        ///<param name="_extras">Additional info to the command</param>
        public static string GetCommand(this Enum value, params object[] _parameters)
        {
            // Get the type
            Type type = value.GetType();

            // Get fieldinfo for this type
            FieldInfo? fieldInfo = type.GetField(value.ToString());

            // Get the stringvalue attributes
            CommandAttribute[]? attribs = null;
            if (fieldInfo != null)
                attribs = fieldInfo.GetCustomAttributes(typeof(CommandAttribute), false) as CommandAttribute[];

            string _add = "";
            foreach (object _s in _parameters)
            {
                _add += "," + _s;
            }

            // Return the first if there was a match.
            if (attribs != null)
                return attribs[0].CommandValue + _add;
            else
                return _add;
        }
    }

    /// <summary>
    /// Attribute to store a string (command) with an enum value
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public class CommandAttribute : Attribute
    {
        /// <summary>
        /// Init a Command Attribute, with the command
        /// </summary>
        public CommandAttribute(string _command)
        {
            CommandValue = _command;
        }

        /// <summary>
        /// Holds the Command string
        /// </summary>
        public string CommandValue { get; protected set; }
    }
}