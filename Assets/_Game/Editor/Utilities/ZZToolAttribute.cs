using System;

namespace ZZ
{
    /// <summary>
    /// Registers a parameterless static editor command in the ZZ toolbox.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, Inherited = false)]
    internal sealed class ZZToolAttribute : Attribute
    {
        public ZZToolAttribute(
            string category,
            string displayName,
            int order,
            string confirmationMessage = null)
        {
            Category = category;
            DisplayName = displayName;
            Order = order;
            ConfirmationMessage = confirmationMessage;
        }

        public string Category { get; }

        public string DisplayName { get; }

        public int Order { get; }

        public string ConfirmationMessage { get; }
    }
}
