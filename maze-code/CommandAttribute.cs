// |||| NAVIGATION - Custom attribute for commands for better code structure ||||
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

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
        /// Will get the command for a certain enum value that has been assigned the command attribute
        /// </summary>
        ///<param name="_extras">Additional info to the command</param>
        public static string GetCommand(this Enum value, string _extras)
        {
            // Get the type
            Type type = value.GetType();

            // Get fieldinfo for this type
            FieldInfo? fieldInfo = type.GetField(value.ToString());

            // Get the stringvalue attributes
            CommandAttribute[]? attribs = null;
            if (fieldInfo != null)
                attribs = fieldInfo.GetCustomAttributes(typeof(CommandAttribute), false) as CommandAttribute[];

            _extras = ',' + _extras;
            // Return the first if there was a match.
            if (attribs != null)
                return attribs[0].CommandValue + _extras;
            else
                return _extras;
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
            this.CommandValue = _command;
        }

        /// <summary>
        /// Holds the Command string
        /// </summary>
        public string CommandValue { get; protected set; }
    }
}

namespace SerialConsole
{
    [Serializable]
    public class NonexistantRampException : Exception
    {
        public NonexistantRampException()
            : base("This ramp does not exist")
        { }

        public NonexistantRampException(string message)
            : base(message)
        { }

        public NonexistantRampException(string message, Exception innerException)
            : base(message, innerException)
        { }
    }
}
