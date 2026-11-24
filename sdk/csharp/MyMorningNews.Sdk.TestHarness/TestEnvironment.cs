namespace MyMorningNews.Sdk.TestHarness
{
    using System;

    /// <summary>
    /// Harness settings from the command line and environment. Command-line arguments override environment variables, which
    /// override the defaults.
    /// </summary>
    public class TestEnvironment
    {
        /// <summary>
        /// Server base URL. Default http://127.0.0.1:12100 (MYMORNINGNEWS_BASE_URL, --base-url).
        /// </summary>
        public string BaseUrl { get; set; } = "http://127.0.0.1:12100";

        /// <summary>
        /// Administrator email. Default admin@mymorningnews (MYMORNINGNEWS_EMAIL, --email).
        /// </summary>
        public string Email { get; set; } = "admin@mymorningnews";

        /// <summary>
        /// Administrator password. Default password (MYMORNINGNEWS_PASSWORD, --password).
        /// </summary>
        public string Password { get; set; } = "password";

        /// <summary>
        /// Tenant to sign in to. Default default (--tenant).
        /// </summary>
        public string Tenant { get; set; } = "default";

        /// <summary>
        /// Second user for the cross-user check. Default demo@mymorningnews.
        /// </summary>
        public string DemoEmail { get; set; } = "demo@mymorningnews";

        /// <summary>
        /// Second user's password. Default password.
        /// </summary>
        public string DemoPassword { get; set; } = "password";

        /// <summary>
        /// Run cases that call real models or start research runs (they spend money). Default false (--with-run).
        /// </summary>
        public bool WithRun { get; set; } = false;

        /// <summary>Run only transport/serialization cases without a live server. Default false.</summary>
        public bool Offline { get; set; } = false;

        /// <summary>
        /// Print each step. Default false (--verbose).
        /// </summary>
        public bool Verbose { get; set; } = false;

        /// <summary>
        /// Prefix for every name the harness creates, so leftovers are easy to find.
        /// </summary>
        public string Prefix { get; set; } = "SDK-csharp-";

        /// <summary>
        /// Instantiate with defaults.
        /// </summary>
        public TestEnvironment()
        {
        }

        /// <summary>
        /// Build the environment from environment variables and arguments.
        /// </summary>
        /// <param name="args">Command-line arguments.</param>
        /// <returns>Environment.</returns>
        /// <exception cref="ArgumentNullException">args is null.</exception>
        /// <exception cref="ArgumentException">An argument is unknown or missing its value.</exception>
        public static TestEnvironment Parse(string[] args)
        {
            ArgumentNullException.ThrowIfNull(args);
            TestEnvironment env = new TestEnvironment();
            string? v = Environment.GetEnvironmentVariable("MYMORNINGNEWS_BASE_URL");
            if (!String.IsNullOrWhiteSpace(v)) env.BaseUrl = v.Trim();
            v = Environment.GetEnvironmentVariable("MYMORNINGNEWS_EMAIL");
            if (!String.IsNullOrWhiteSpace(v)) env.Email = v.Trim();
            v = Environment.GetEnvironmentVariable("MYMORNINGNEWS_PASSWORD");
            if (!String.IsNullOrEmpty(v)) env.Password = v;

            for (int i = 0; i < args.Length; i++)
            {
                string a = args[i];
                switch (a)
                {
                    case "--base-url": env.BaseUrl = Value(args, ref i, a); break;
                    case "--email": env.Email = Value(args, ref i, a); break;
                    case "--password": env.Password = Value(args, ref i, a); break;
                    case "--tenant": env.Tenant = Value(args, ref i, a); break;
                    case "--with-run": env.WithRun = true; break;
                    case "--offline": env.Offline = true; break;
                    case "--verbose": env.Verbose = true; break;
                    default: throw new ArgumentException("Unknown argument " + a + ". Use --base-url, --email, --password, --tenant, --with-run, or --verbose.");
                }
            }

            return env;
        }

        private static string Value(string[] args, ref int i, string name)
        {
            if (i + 1 >= args.Length || String.IsNullOrWhiteSpace(args[i + 1])) throw new ArgumentException(name + " needs a value.");
            i++;
            return args[i];
        }
    }
}
