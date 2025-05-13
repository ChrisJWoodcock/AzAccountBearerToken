namespace AzAccountBearerToken
{
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using System;
    using System.Diagnostics;
    using static AzAccountBearerToken.Program;

    class Program
    {
        static void Main(string[] args)
        {
            var config = ReadConfig();

            config.ResourceDomain = GetUserInputForResource(config.ResourceDomain);
            config.Environment = GetUserInputForEnvironment(config.ResourceDomain, config.Environment);

            bool isResourceDomainPresent = IsDomainPresentInAccountList(config.ResourceDomain);

            if (!isResourceDomainPresent)
            {
                RunPowershellCommand("az login");

                // re-check after login
                isResourceDomainPresent = IsDomainPresentInAccountList(config.ResourceDomain);

                if (!isResourceDomainPresent)
                {
                    Console.WriteLine($"Failed to login with the {config.ResourceDomain} domain. Exiting...");
                    return;
                }
            }

            var endpointSuffix = PromptForEndpointSuffix(config);
            var command = $"az account get-access-token --resource=https://{config.ResourceDomain}/das-{config.Environment}-{endpointSuffix}";
            string token = RunPowershellCommand(command);
            Console.WriteLine(token);
            WriteConfig(config);
            Console.ReadKey();
        }

        public class Config
        {
            public string? ResourceDomain { get; set; }
            public List<string?> EndpointSuffixHistory { get; set; } = new List<string?>();
            public string? Environment { get; set; }
        }

        private static Config ReadConfig()
        {
            Config? config = null;
            if (File.Exists("config.json"))
            {
                var configContent = File.ReadAllText("config.json");
                if (!string.IsNullOrEmpty(configContent))
                {
                    config = JsonConvert.DeserializeObject<Config>(configContent);
                }
            }

            return config != null ? config : new Config();
        }

        private static void WriteConfig(Config? config)
        {
            if (config != null)
            {
                var configContent = JsonConvert.SerializeObject(config, Formatting.Indented);
                File.WriteAllText("config.json", configContent);
            }
        }

        private static string? PromptWithDefaultValue(string message, string? defaultValue)
        {
            Console.Write($"{message} [Default: {defaultValue}]: ");
            var input = Console.ReadLine()?.Trim();
            return string.IsNullOrEmpty(input) ? defaultValue : input;
        }

        private static string? PromptForEndpointSuffix(Config config)
        {
            Console.WriteLine("Please provide the endpoint suffix:");
            for (int i = 0; i < config.EndpointSuffixHistory.Count; i++)
            {
                Console.WriteLine($"{i + 1}. {config.EndpointSuffixHistory[i]}");
            }
            Console.Write("Enter the number of the endpoint suffix to use, or enter a new one: ");

            var input = Console.ReadLine()?.Trim();
            if (int.TryParse(input, out int choice) && choice >= 1 && choice <= config.EndpointSuffixHistory.Count)
            {
                // User selected from the list
                return config.EndpointSuffixHistory[choice - 1];
            }
            else
            {
                if (!string.IsNullOrEmpty(input) && !config.EndpointSuffixHistory.Contains(input))
                {
                    if (config.EndpointSuffixHistory.Count == 10) // Keep only the last 10 entries
                    {
                        config.EndpointSuffixHistory.RemoveAt(9);
                    }
                    config.EndpointSuffixHistory.Add(input);
                }
                return input;
            }
        }


        private static string? PromptForEndpointSuffix(string? defaultValue)
        {
            var endpointSuffix = PromptWithDefaultValue("Please provide the endpoint suffix (e.g., 'assapi-as-ar')", defaultValue);
            return endpointSuffix;
        }

        static string GetUserInputForResource(string? defaultValue)
        {
            var resourceChoice = PromptWithDefaultValue("Which resource would you like to use? (Enter 'CDS' or 'FCS')", defaultValue);

            if (resourceChoice?.ToUpper() == "CDS" || resourceChoice == "citizenazuresfabisgov.onmicrosoft.com")
            {
                return "citizenazuresfabisgov.onmicrosoft.com";
            }
            else if (resourceChoice?.ToUpper() == "FCS" || resourceChoice == "fcsazuresfabisgov.onmicrosoft.com")
            {
                return "fcsazuresfabisgov.onmicrosoft.com";
            }
            else
            {
                Console.WriteLine("Invalid resource choice. Exiting...");
                Environment.Exit(0);
                return null;
            }
        }

        static string GetUserInputForEnvironment(string resourceDomain, string? defaultValue)
        {
            try
            {
                if (resourceDomain.Contains("citizenazuresfabisgov"))
                {
                    string? citizenEnvironmentChoice = PromptWithDefaultValue("Select the environment: AT, TEST, or TEST2", defaultValue?.ToUpper());
                    return citizenEnvironmentChoice?.ToLower() switch
                    {
                        "at" => "at",
                        "test" => "test",
                        "test2" => "test2",
                        _ => throw new Exception("Invalid environment choice for citizen domain.")
                    };
                }
                else if (resourceDomain.Contains("fcsazuresfabisgov"))
                {
                    string? fcsEnvironmentChoice = PromptWithDefaultValue("Select the environment: PP or PRD: ", defaultValue?.ToUpper());
                    return fcsEnvironmentChoice?.ToLower() switch
                    {
                        "pp" => "pp",
                        "prd" => "prd",
                        _ => throw new Exception($"Invalid environment choice for FCS domain.")
                    };
                }
                else
                {
                    throw new Exception("Invalid domain passed to environment selector.");
                }
            }
            catch(Exception ex)
            {
                Console.WriteLine(ex.ToString());
                throw;
            }
        }

        private static bool IsDomainPresentInAccountList(string resourceDomain)
        {
            var accountListJson = RunPowershellCommand("az account list");

            if (string.IsNullOrWhiteSpace(accountListJson))
            {
                Console.WriteLine("Failed to fetch account list. Exiting...");
                return false;
            }

            var accounts = JArray.Parse(accountListJson);
            bool isResourceDomainPresent = accounts.Any(account =>
                account["user"]?["name"]?.Value<string>()?.EndsWith(resourceDomain) == true &&
                account["isDefault"]?.Value<bool>() == true);

            return isResourceDomainPresent;
        }


        static string RunPowershellCommand(string command)
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "powershell",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    Arguments = $"-NoProfile -Command {command}"
                }
            };

            process.Start();

            var result = process.StandardOutput.ReadToEnd();

            process.WaitForExit();

            return result.Trim();
        }
    }
}
