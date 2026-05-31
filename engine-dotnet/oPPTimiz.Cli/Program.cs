//oPPTimiz is a tool that reduces Office documents size.
//Copyright (C) 2025 EDF
//This program is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.
//This program is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details.
//You should have received a copy of the GNU General Public License along with this program. If not, see https://www.gnu.org/licenses/.

using System;
using oPPTimiz.Core;

namespace oPPTimiz.Cli
{
    internal static class Program
    {
        private const string Usage =
            "oPPTimiz.exe -pptFile source [-compressionLevel [Maximal | Intermediate]] [-keepFile [0 | 1]]";

        private static int Main(string[] args)
        {
            string source = null;
            OptimizationLevel level = OptimizationLevel.Maximal;
            bool keepFile = false;

            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i].ToLowerInvariant())
                {
                    case "-pptfile":
                        source = NextArgument(args, ref i);
                        break;
                    case "-compressionlevel":
                        string value = NextArgument(args, ref i);
                        if (string.Equals(value, "Intermediate", StringComparison.OrdinalIgnoreCase))
                            level = OptimizationLevel.Intermediate;
                        else if (string.Equals(value, "Maximal", StringComparison.OrdinalIgnoreCase))
                            level = OptimizationLevel.Maximal;
                        break;
                    case "-keepfile":
                        keepFile = NextArgument(args, ref i) == "1";
                        break;
                }
            }

            if (string.IsNullOrEmpty(source))
            {
                Console.Error.WriteLine(Usage);
                return 1;
            }

            try
            {
                string destination;
                OptimizationResult result = Optimizer.OptimizeFile(source, level, keepFile, null, out destination);

                Console.WriteLine("Fichier optimise : " + destination);
                Console.WriteLine("  Taille initiale  : " + HumanSize(result.InitialSize));
                Console.WriteLine("  Taille optimisee : " + HumanSize(result.NewSize));
                Console.WriteLine("  Gain             : " + HumanSize(result.Gain) + " (" + result.Percentage + "%)");
                Console.WriteLine("  Images traitees  : " + result.ImagesOptimized);
                Console.WriteLine("  Medias supprimes : " + result.MediaRemoved);
                return 0;
            }
            catch (Exception error)
            {
                Console.Error.WriteLine("oPPTimiz: " + error.Message);
                return 1;
            }
        }

        private static string NextArgument(string[] args, ref int index)
        {
            if (index + 1 >= args.Length)
                return null;
            index++;
            return args[index];
        }

        private static string HumanSize(long octets)
        {
            string[] units = { "octets", "Ko", "Mo", "Go" };
            double value = octets;
            int unit = 0;
            while (value >= 1024 && unit < units.Length - 1)
            {
                value /= 1024;
                unit++;
            }
            return unit == 0 ? value + " " + units[unit] : Math.Round(value, 1) + " " + units[unit];
        }
    }
}
