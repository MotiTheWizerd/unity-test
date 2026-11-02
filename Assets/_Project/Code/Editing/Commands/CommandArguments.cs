using System;
using System.Collections;
using System.Globalization;

namespace Forge.Editing.Commands
{
    /// <summary>
    /// Checks a command's arguments against their <see cref="ParamAttribute"/>s, nested ones included, before it
    /// runs. A broken rule is a <see cref="CommandException"/> naming the argument as callers write it
    /// (e.g. "objects[2].scale"), so the agent can correct itself.
    /// </summary>
    public static class CommandArguments
    {
        public static void Validate(object arguments)
        {
            if (arguments == null)
                throw new CommandException("The command needs its arguments.");
            ValidateObject(arguments, string.Empty);
        }

        static void ValidateObject(object owner, string prefix)
        {
            foreach (var parameter in CommandParameters.Of(owner.GetType()))
                Check(parameter.ValueOf(owner), parameter, prefix + parameter.Name);
        }

        static void Check(object value, CommandParameter parameter, string path)
        {
            if (value == null)
            {
                if (!parameter.Info.Optional)
                    throw new CommandException($"'{path}' is required.");
                return;
            }

            switch (parameter.Kind)
            {
                case ParameterKind.Integer:
                case ParameterKind.Number:
                    CheckNumber(Convert.ToDouble(value, CultureInfo.InvariantCulture), parameter.Info, path);
                    break;
                case ParameterKind.String:
                    // An empty optional text means the same as leaving it out.
                    if (!parameter.Info.Optional && ((string)value).Trim().Length == 0)
                        throw new CommandException($"'{path}' must not be empty.");
                    break;
                case ParameterKind.Object:
                    ValidateObject(value, path + ".");
                    break;
                case ParameterKind.List:
                    CheckList((IList)value, parameter, path);
                    break;
            }
        }

        static void CheckList(IList list, CommandParameter parameter, string path)
        {
            var info = parameter.Info;
            if (info.MinItems > 0 && list.Count < info.MinItems)
                throw new CommandException($"'{path}' needs at least {info.MinItems} item(s); it has {list.Count}.");
            if (info.MaxItems > 0 && list.Count > info.MaxItems)
                throw new CommandException($"'{path}' takes at most {info.MaxItems} item(s); it has {list.Count}.");

            for (var i = 0; i < list.Count; i++)
            {
                var item = list[i];
                var itemPath = $"{path}[{i}]";
                if (item == null)
                    throw new CommandException($"'{itemPath}' is empty.");

                switch (parameter.ItemKind)
                {
                    case ParameterKind.Object:
                        ValidateObject(item, itemPath + ".");
                        break;
                    case ParameterKind.Integer:
                    case ParameterKind.Number:
                        CheckFinite(Convert.ToDouble(item, CultureInfo.InvariantCulture), itemPath);
                        break;
                    case ParameterKind.String:
                        if (((string)item).Trim().Length == 0)
                            throw new CommandException($"'{itemPath}' must not be empty.");
                        break;
                }
            }
        }

        static void CheckNumber(double number, ParamAttribute info, string path)
        {
            CheckFinite(number, path);
            if (!double.IsNaN(info.Minimum) && number < info.Minimum)
                throw new CommandException($"'{path}' must be at least {Format(info.Minimum)}; it is {Format(number)}.");
            if (!double.IsNaN(info.Maximum) && number > info.Maximum)
                throw new CommandException($"'{path}' must be at most {Format(info.Maximum)}; it is {Format(number)}.");
        }

        static void CheckFinite(double number, string path)
        {
            if (double.IsNaN(number) || double.IsInfinity(number))
                throw new CommandException($"'{path}' must be a finite number.");
        }

        static string Format(double number) => number.ToString("0.###", CultureInfo.InvariantCulture);
    }
}
