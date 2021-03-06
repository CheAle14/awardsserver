﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Win32;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Newtonsoft.Json;
using System.Net;
using System.Net.Sockets;
using System.Net.Http;

//get ready for some seemingly obvious questions
//ctrl-f "??" to find what might be confusing
// 
namespace AwardsServer
{
    public class Program
    {
        public static ServerUI.UIForm ServerUIForm;
        public static SocketHandler Server; // Handles the connection and essentially interfaces with the TCP-side of things
        public static DatabaseStuffs Database; // database related things
        public static ServerUI.WebsiteHandler WebServer;
        public static GithubDLL.GithubClient Github;
        public static GithubDLL.Entities.Repository AwardsRepository;
        public const string RepoRegex = @"(?<=repos)\/.*\/.*";
        public const string IssueFindRegex = @"\S*\/\S*#\d+";
        public static string OverridePassword = null;

        public static List<BugReport.BugReport> BugReports = new List<BugReport.BugReport>();

        [AttributeUsage(AttributeTargets.Field, AllowMultiple = false)] //?? - Determines where the below attribute can be used; in our case, we just need it on Fields (ie, variables)
        public class OptionAttribute : Attribute //what is this for?? does it 'Constructs the information for an Option.'? // the class holds info on the options, and the Attribute allows it to be put in the [ ]
        {
            public readonly string Name;
            public readonly string Description;
            public readonly object DefaultValue;
            public readonly bool ReadOnly;
            public readonly bool Sensitive;
            /// <summary>
            /// Constructs the information for an Option.
            /// </summary>
            /// <param name="description">Displayed on UI Form: what this option does</param>
            /// <param name="name">Internal/short name for this option</param>
            /// <param name="defaultValue">Default value for the option</param>
            /// <param name="readOnly">Determines whether the option can be edited in the UI (note, can still be edited in code, and via registry)</param>
            /// <param name="sensitive">Determines whether a text/string should be displayed</param>
            public OptionAttribute(string description, string name, object defaultValue, bool readOnly = false, bool sensitive = false)
            {
                Name = name;
                Description = description;
                DefaultValue = defaultValue;
                ReadOnly = readOnly;
                Sensitive = sensitive;
            }
        }
        public static class Options
        {
            [Option("Relative/Absolute path for the file used to contain the Server's IP", "Path of ServerIP file", @"..\..\..\ServerIP", true)]
            public static string ServerIP_File_Path;

            [Option("Path for votes to be saved to in text-file format", "Backup vote text file path", @"..\..\..\RawVotes.log", true)]
            public static string ServerTextFileVotes_Path;

            [Option("Maximum number of students to list in a name query response", "Max students for query", 10)]
            public static int Maximum_Query_Response;

            [Option("Is the same username permitted to be connected at the same time", "Allow identical usernames", false)]
            public static bool Simultaneous_Session_Allowed;

            [Option("Allow student data to be modified even after someone joins", "Allow data modify", false)]
            public static bool Allow_Modifications_When_Voting;

            [Option("Maximum before queue begins.", "Queue threshhold", 15)]
            public static int Maximum_Concurrent_Connections;

            [Option("Time (in seconds) between each heartbeat message is sent", "Time (s) between heartbeat", 5)]
            public static int Time_Between_Heartbeat;

            [Option("Whether it should display when a message is recieved", "Whether console shows message recieved", true)]
            public static bool Display_Recieve_Client;

            [Option("Whether it should display when a message is sent", "Whether console shows sent messages", true)]
            public static bool Display_Send_Client;

            [Option("Any severity below this is not shown in the UI", "Lowest severity displayed", Logging.LogSeverity.Debug)]
            public static Logging.LogSeverity Only_Show_Above_Severity;

            [Option("Allow someone other than server to see the winners", "Allow see redacted", false)]
            public static bool Allow_NonLocalHost_WebConnections;

            [Option("Is the web server serving files/listening for connections?", "Web site status", false)]
            public static bool WebSever_Enabled;

            [Option("Can the server manually vote on behalf of a user via its 'Manual Vote' tab", "Can server save a vote", true)]
            public static bool Allow_Manual_Vote;

            [Option("Github authentication token", "Github authentification token", "", false, true)]
            public static string Github_AuthToken;

            [Option("Masterlist:: Username @ Password", "Masterlist user & password, seperator @", "", false, true)]
            public static string Masterlist_UserPassword;

            [Option("Kicked users are unable to rejoin", "Kick is infact a ban", false)]
            public static bool Perm_Block_Kicked_Users;

            [Option("Path to folder where client logs of bug reports are stored.", "Bug report log folder", @"buglogs\")]
            public static string Client_Bug_Logs_Folder_Path;

            [Option("Default text editor to open with", "Text editor", "wordpad.exe")]
            public static string DEFAULT_TEXT_EDITOR;

            [Option("Default web browser to open with", "Web browser", "chrome.exe")]
            public static string DEFAULT_WEB_BROWSER;

        }

        private const string MainRegistry = "HKEY_CURRENT_USER\\AwardsProgram\\Server";
        public static void SetOption(string key, string value) //?? - Sets the registry value, its all saved as strings and casted when read
        {
            Microsoft.Win32.Registry.SetValue(MainRegistry, key, value); //?? - built in function, sets the value thats all i know
        }
        public static T Convert<T>(string input) //converts one object type to another ?? // it does, but not sure if i am actually using it
        {
            try
            {
                var converter = TypeDescriptor.GetConverter(typeof(T));
                if (converter != null)
                {
                    // Cast ConvertFromString(string text) : object to (T)
                    return (T)converter.ConvertFromString(input);
                }
                return default(T);
            }
            catch (NotSupportedException)
            {
                return default(T);
            }
        }

        public static string GetOption(string key, string defaultValue) //returns... option as a string?? // yep: tries to get value, if not, returns the default
        {
            var item = Microsoft.Win32.Registry.GetValue(MainRegistry, key, defaultValue);
            if (item == null)
                return defaultValue;
            return (string)item;
        }

        public static bool TryGetUser(string username, out User user) //checks if the user exits (+assigns them to 'user' if true)??
        { // this is similar to a dictionary's "TryGetValue(key, out value)" function
            // it will attempt to find the key, and set 'value' equal to the saved/stored data
            // note the "out" word at the top there, that allows the variable to be set within this function
            // essentially, it will return true and the "user" will become the proper value if the user is found
            // or if not, it returns false and the "user" is set to null
            user = null;
            if (string.IsNullOrWhiteSpace(username))
                return false;
            if(Database.AllStudents.ContainsKey(username))
            {
                user = Database.AllStudents[username];
                return true;
            }
            return false;
        }
        public static User GetUser(string username) //the above checks if the user exists, this returns the user
        { // you should use EITHER this above OR this function - theres no need to use both
            // i think this function is just used  for a LINQ statment (ie, list.FirstOrDefault(x => ....)), since i couldnt use the above
            TryGetUser(username, out User user);
            return user;
        }

        private static readonly object _bugLock = new object();
        public static void SaveBugs()
        {
            lock(_bugLock)
                System.IO.File.WriteAllText("bugreports.json", JsonConvert.SerializeObject(Program.BugReports));
        }
        public static void LoadBugs()
        {
            lock (_bugLock)
            {
                string content = "";
                try
                {
                    content = System.IO.File.ReadAllText("bugreports.json");
                }
                catch (System.IO.FileNotFoundException) {
                    System.IO.File.CreateText("bugreports.json");
                }
                if (string.IsNullOrWhiteSpace(content))
                    BugReports = new List<BugReport.BugReport>();
                else
                    BugReports = JsonConvert.DeserializeObject<List<BugReport.BugReport>>(content);
            }

        }

        /// <summary>
        /// Returns the current computer's local ip address within its current network
        /// </summary>
        public static string GetLocalIPAddress()
        {
            var host = System.Net.Dns.GetHostEntry(System.Net.Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                {
                    return ip.ToString();
                }
            }
            throw new Exception("No network adapters with an IPv4 address in the system!");
        }


        // Console window closing things:
        // Yeah, I just copy-pasted the following
        // Essentially its registering an event with window's kernal, and listening for when that gets fired
        private delegate bool ConsoleEventDelegate(int eventType); //?? i mean this is a new level of ??
        [DllImport("kernel32.dll", SetLastError = true)] //??
        private static extern bool SetConsoleCtrlHandler(ConsoleEventDelegate callback, bool add); //??
        static ConsoleEventDelegate handler; //handles... anything??
        static bool ConsoleEventCallback(int eventType) //why is this used ?? - this is where the above 'console window closing' callback gets fired
        {
            if (eventType == 2) //what's event type 2?? -it's the window closing right - yes.. again copy/paste so
            {
                // code to run here
                Logging.Log(new Logging.LogMessage(Logging.LogSeverity.Severe, "Console window closing..")); 
                try
                {
                    Database.Disconnect();
                } catch (Exception ex)
                {
                    Logging.Log("CloseConn", ex);
                }
            }
            return false;
        }

        public static event EventHandler<string> ConsoleInput;
        static void Main(string[] args)
        {
            handler = new ConsoleEventDelegate(ConsoleEventCallback);
            SetConsoleCtrlHandler(handler, true); // this line & above handle the console window closing
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException; //?? - allows us to log any errors that completely crash the server
            // without the above, any error that is not within a "try..except" would simply cause the console window to close without any log or message.
            Logging.Log(Logging.LogSeverity.Severe, "[README] Available at: https://y11awards.page.link/readme");
            Logging.Log(Logging.LogSeverity.Severe, "[ISSUES] Reportable at: https://y11awards.page.link/issues");
            Logging.Log(Logging.LogSeverity.Severe, "[HELP]   Documentation at: https://masterlist.uk.ms/wiki/index.php/Awards");

            Logging.Log(Logging.LogSeverity.Info,  "Loading existing categories...");
            Database = new DatabaseStuffs();
            Database.Connect();
            Database.Load_All_Votes();
            if (Database.AllStudents.Count == 0)
            {
                Logging.Log(Logging.LogSeverity.Error, "No students have been loaded. Assuming that this is an error.");
                Logging.Log(Logging.LogSeverity.Error, "No students have been loaded. Assuming that this is an error.");
                Console.ReadLine();
                Logging.Log(Logging.LogSeverity.Error, "This error will continue to occur until atleast one student is added to the 'Database.accdb' file");
                Console.ReadLine();
                return; // closes
            }
#if DEBUG
           
            var st = new User(Environment.UserName.ToLower(), "Local", "Host", "1010");
            st.Flags.Add(Flags.System_Operator);
            st.Flags.Add(Flags.View_Online);
            if (!Database.AllStudents.ContainsKey(st.AccountName)) //if the user is not in the database
                Database.AllStudents.Add(st.AccountName, st); //add the user
#endif

            Logging.Log($"Loaded {Database.AllStudents.Count} students and {Database.AllCategories.Count} categories.");



            Logging.Log("Starting socket listener... @ " + GetLocalIPAddress());
            Server = new SocketHandler();
            Logging.Log("Started. Ready to accept new connections.");

            // Open UI form..
            System.Threading.Thread uiThread = new System.Threading.Thread(RunUI);
            uiThread.Start();

            System.Threading.Thread apiThread = new System.Threading.Thread(updateServerIP);
            apiThread.Start();

            ConsoleInput += Program_ConsoleInput; // listens to event only *after* we have started everything
            while(Server.Listening)
            {
                var str = Console.ReadLine(); // reads line and stores to var
                try
                {
                    ConsoleInput?.Invoke(null, str); // invokes any places that are listening to the event, passing the input
                }
                catch (Exception ex)
                {
                    Logging.Log("Console", ex);
                    Logging.Log(Logging.LogSeverity.Console, "Your input caused an error. Double check you typed it correctly, and try again");
                }
            }
            Logging.Log(Logging.LogSeverity.Severe, "Server has exited its main listening loop");
            Logging.Log(Logging.LogSeverity.Error, "Server closed.");
            while(true)
            { // pause at end so they can read console
                Console.ReadLine();
            }
        }

        private static void updateServerIP()
        {
            try
            {
                using (HttpClient client = new HttpClient())
                {
                    client.BaseAddress = new Uri("http://localhost:8887/");
                    string ip = GetLocalIPAddress();
                    if (ip.StartsWith("10.") && false)
                        throw new Exception("Expected local computer IP to begin '10.' but it doesnt: " + ip);
                    HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, "awards/ip?token=bqdDBD&newIp=" + ip); // GetLocalIPAddress());
                    request.Headers.Add("User-Agent", $"awards-{Environment.UserName}");
                    Random rnd = new Random(DateTime.Now.Millisecond * DateTime.Now.DayOfYear);
                    int pas = rnd.Next(0, 100000);
                    OverridePassword = pas.ToString("00000");
                    request.Headers.Add("X-Override", OverridePassword);
                    var r = client.SendAsync(request).GetAwaiter().GetResult();
                    string str = r.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                    string apiId = "<n/a>";
                    if(r.Headers.TryGetValues("X-API-ID", out var values))
                    {
                        apiId = values.FirstOrDefault();
                    }
                    if(r.IsSuccessStatusCode)
                    {
                        Logging.Log(Logging.LogSeverity.Info, "API reports that IP has been updated, ref: " + apiId);
                    } else
                    {
                        Logging.Log(Logging.LogSeverity.Error, "IP was not set correctly: " + str);
                        Logging.Log(Logging.LogSeverity.Severe, "API ID reference: " + apiId);
                    }
                }
            } catch (Exception ex)
            {
                Logging.Log("apiThread", ex);
            }
        }

        private static void Program_ConsoleInput(object sender, string e)
        {
            if (e.StartsWith("/"))
                e = e.Substring(1);
            e = e.ToLower();
            if (e == "remove_all_votes")
            {
                //Logging.Log(Logging.LogSeverity.Console, "Command has been removed.");
                if (MessageBox.Show("Are you sure you want to REMOVE EVERY SINGLE VOTE?", "Remove All Votes", MessageBoxButtons.YesNo) == DialogResult.Yes)
                {
                    foreach (var cat in Database.AllCategories)
                    {
                        Database.ExecuteCommand($"DELETE FROM Category{cat.Key} WHERE True = True"); // removes all records
                    }
                    Database.Load_All_Votes();
                    Logging.Log(Logging.LogSeverity.Console, "Removed all users votes.");
                    try
                    {
                        ServerUIForm.Close();
                        ServerUIForm.Dispose(); // close the UI so it reloads
                    } catch { } // dont need to error catch this
                }
            } else if(e == "copy_winners")
            {
                Logging.Log(Logging.LogSeverity.Console, "Command disabled");
            } else if(e.StartsWith("op"))
            {
                e = e.Substring(3).Trim(); // remove "op "
                if(Program.TryGetUser(e, out User user))
                {
                    if (user.Flags.Contains(Flags.System_Operator))
                    {
                        user.Flags.Remove(Flags.System_Operator);
                        Logging.Log(Logging.LogSeverity.Console, "Removed sysop from " + user.AccountName);
                    }
                    else
                    {
                        user.Flags.Add(Flags.System_Operator);
                        Logging.Log(Logging.LogSeverity.Console, "Given sysop to " + user.AccountName);
                    }
                    Database.ExecuteCommand($"UPDATE UserData SET Flags = '{string.Join(";", user.Flags)}' WHERE UserName = '{user.AccountName}'");
                    if (user.Connection != null)
                        user.Connection.ReSendAuthentication();
                }
                else
                {
                    Logging.Log(Logging.LogSeverity.Console, "Unknown user account: " + e);
                }
            } else if (e.StartsWith("chat"))
            {
                if(e.Trim() == "chat")
                {
                    Logging.Log(Logging.LogSeverity.Console, "You must actually type a message to send.");
                } else
                {
                    e = e.Substring("chat".Length + 1);
                    SendAdminChat(new AdminMessage("Server", SocketHandler.Authentication.Sysadmin, e));
                }
            } else if(e.StartsWith("load votes"))
            {
                System.IO.FileInfo file = new System.IO.FileInfo(Options.ServerTextFileVotes_Path);
                if(MessageBox.Show($"Are you sure you want to add in the votes from the file:" + file.FullName, "Add votes: Confirm", MessageBoxButtons.OKCancel) == DialogResult.OK)
                {
                    string[] lineByLine = System.IO.File.ReadAllLines(file.FullName);
                    Func<string, bool> warn = (message) =>
                    {
                        Logging.Log(Logging.LogSeverity.Warning, message, "LoadVotes");
                        return true;
                    };
                    Func<Tuple<User, User>, User, bool> contains = (tuple, usr) =>
                    {
                        if (tuple.Item1?.AccountName == usr.AccountName)
                            return true;
                        if (tuple.Item2?.AccountName == usr.AccountName)
                            return true;
                        return false;
                    };
                    foreach (string line in lineByLine)
                    { 
                        string[] firstSplit = line.Split('/');
                        // name/votes
                        if(Program.TryGetUser(firstSplit[0], out User voter))
                        {
                            string[] votes = firstSplit[1].Split('#');
                            int category = 0;
                            foreach(string vote in votes)
                            {
                                if (string.IsNullOrWhiteSpace(vote))
                                    continue;
                                category++;
                                Category cat = Database.AllCategories[category];
                                var votesDoneAlready = cat.GetVotesBy(voter);
                                string[] names = vote.Split(';');
                                // should always give two-element array
                                // unless the string is null or empty, which is handled.
                                if(Program.TryGetUser(names[0], out User first))
                                {
                                    if(!contains(votesDoneAlready, first))
                                    {
                                        warn($"Cat:{cat.ID} | Load Vote for {first.AccountName} by {voter.AccountName}");
                                        Database.AddVoteFor(category, first, voter);
                                    }
                                }
                                if(Program.TryGetUser(names[1], out User second))
                                {
                                    if (!contains(votesDoneAlready, second))
                                    {
                                        warn($"Cat:{cat.ID} | Load Vote for {second.AccountName} by {voter.AccountName}");
                                        Database.AddVoteFor(category, second, voter);
                                    }
                                }
                            }
                        } else
                        {
                            warn("Unknown user: " + firstSplit[0] + "; full line: " + line);
                        }
                    }

                }
            } else if(e.StartsWith("save votes"))
            {
                System.IO.FileInfo file = new System.IO.FileInfo(Options.ServerTextFileVotes_Path);
                if(MessageBox.Show($"Are you sure you want to OVERWRITE the votes in:\r\n" + file.FullName, "Overwrite votes: Confirm", MessageBoxButtons.OKCancel) == DialogResult.OK)
                {
                    _saveVotes();
                }
            } else if(e.StartsWith("count") || e.StartsWith("num"))
            {
                Logging.Log(Logging.LogSeverity.Console, $"In queue:     {SocketHandler.ClientQueue.Count}\r\n" +
                    $"Cur voting:   {SocketHandler.CurrentClients.Count}\r\n" +
                    $"All students: {SocketHandler.CurrentClients.Count + SocketHandler.ClientQueue.Count}");
            } else if(e == "upload votes")
            {
                System.IO.FileInfo file = new System.IO.FileInfo(Options.ServerTextFileVotes_Path);
                string url = $"https://{Program.Options.Masterlist_UserPassword}@masterlist.uk.ms/secure/dev/upload.html";
                Logging.Log(Logging.LogSeverity.Console, $"Location of votes: {file.FullName}");
                System.Diagnostics.Process.Start(Program.Options.DEFAULT_WEB_BROWSER, url);
            } else if(e.StartsWith("sql"))
            {
                e = e.Replace("sql ", "");
                var rows = Database.ExecuteCommand(e);
                Logging.Log(Logging.LogSeverity.Console, $"Rows affected: {rows}");
            }
        }

        static string _saveVotes()
        {
            System.IO.FileInfo file = new System.IO.FileInfo(Options.ServerTextFileVotes_Path);
            string contents = "";
            foreach (var student in Database.AllStudents)
            {
                string str = $"{student.Key}/";
                bool yesVote = false;
                foreach (var category in Database.AllCategories.Values)
                {
                    var votes = category.GetVotesBy(student.Value);
                    if (votes.Item1 != null || votes.Item2 != null)
                        yesVote = true;
                    str += $"{(votes.Item1?.AccountName ?? "")};{(votes.Item2?.AccountName ?? "")}#";
                }
                if (yesVote) // only append when they have actually voted
                    contents += str + "\r\n";
            }
            System.IO.File.WriteAllText(file.FullName, contents);
            return contents;
        }

        public static void SendAdminChat(AdminMessage message)
        {
            foreach (var admin in SocketHandler.AdminClients)
            {
                admin.Send(message.ToSend());
            }
            Logging.Log(Logging.LogSeverity.Console, message.Content, "Adm/" + message.From);
        }

        private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        { //logs any unhandled exceptions
            Logging.Log(new Logging.LogMessage(Logging.LogSeverity.Severe, "Unhandled", (Exception)e.ExceptionObject));
        }
        private static void RunUI()
        {
            bool first = false; //??why
            while (Server.Listening)
            {
                if(ServerUIForm != null) // since the user can close it, we need to check if it is open first
                {
                    ServerUIForm.Dispose(); // and if it is open, then we should close the form
                }
                ServerUIForm = new ServerUI.UIForm(); // and make a new one
                if(!first) // this allows the user to close the ui form ONCE, before they have all edit abilities removed
                { // since the data/etc only updates when it is closed, it may be desired to close it and edit again
                    first = true;
                    ServerUIForm.PermittedStudentEdits(ServerUI.UIForm.EditCapabilities.All);
                } else
                {
                    ServerUIForm.PermittedStudentEdits(ServerUI.UIForm.EditCapabilities.None);
                }
                ServerUIForm.ShowDialog();
                Logging.Log(Logging.LogSeverity.Debug, "UI Form closed; reopening to regenerate data. If you want to close the server, close the Console");
            }
        }
    }

    // Shared stuff that will be used across multiple files.
    public class User
    {
        public readonly string AccountName; // eg 'cheale14'
        public readonly string FirstName;
        public readonly string LastName;
        public readonly string Tutor;
        [Obsolete("Removed. See: TheGrandCoding/awardsserver#50", true)]
        public readonly char Sex;
        public bool HasVoted => Program.Database.AlreadyVotedNames.Contains(AccountName);
        public string FullName => FirstName + " " + LastName;

        public SocketHandler.SocketConnection Connection; // could be null

        /// <summary>
        /// A list of values that indicate special options.
        /// </summary>
        public List<string> Flags = new List<string>();

        public User(string accountName, string firstName, string lastName, string tutor) 
        {//creating a new user
            AccountName = accountName;
            FirstName = firstName;
            LastName = lastName;
            Tutor = tutor;
        }
        public override string ToString()
        {
            return this.ToString("AN: FN LN (TT)"); // $"{AccountName}: {FirstName} {LastName} ({Tutor})";
        }
        /// <summary>
        /// AN = Account Name
        /// FN = First Name
        /// LN = Last Name
        /// TT = Tutor
        /// </summary>
        /// <param name="format"></param>
        /// <returns></returns>
        public string ToString(string format)
        {
            format = format.Replace("AN", "{0}");
            format = format.Replace("FN", "{1}");
            format = format.Replace("LN", "{2}");
            format = format.Replace("TT", "{3}");
            return string.Format(format, this.AccountName, this.FirstName, this.LastName, this.Tutor);
        }
    }
    public class Category
    {
        public readonly int ID; // each category should have a integer assigned (from 1 to 15 for example)
        public readonly string Prompt; // eg 'most likely to become Prime Minister'
        private static int __id = 0;
        public Category(string prompt, int id = -1)
        {
            ID = System.Threading.Interlocked.Increment(ref __id);
            if (id > -1)
            { // allows you to manually set the ID (eg, from database)
                ID = id;
            }
            Votes = new Dictionary<string, List<User>>();
            Prompt = prompt;
        }
        public Dictionary<string, List<User>> Votes; // key: AccountName of user, list is all the users that voted for that person.

        /// <summary>
        /// Returns the keys of the Votes dict from highest to lowest.
        /// </summary>
        /// <returns></returns>
        public List<string> SortVotes() //in ascending order
        {
            var sortedDict = from entry in Votes orderby entry.Value.Count ascending select entry.Key;
            // yay for linq.
            return sortedDict.ToList();
        }

        public List<KeyValuePair<string, List<User>>> _inPosition(int position)
        {
            if (this.Votes.Count == 0)
                return new List<KeyValuePair<string, List<User>>>();
            var sorted = this.Votes.OrderByDescending(x => x.Value.Count);
            if (position >= sorted.Count())
                position = sorted.Count() - 1; // fix the index to prevent out of range
            var atIndex = sorted.ElementAt(position);
            var allWithThatValue = sorted.Where(x => x.Value.Count == atIndex.Value.Count);
            return allWithThatValue.ToList();
        }

        /// <summary>
        /// Returns the person with the highest vote, or the list of people tied to the highest vote
        /// Also, if giveSecondHighest is true, will return the person/people tied to the second-highest vote.
        /// </summary>
        public Tuple<List<User>, int> HighestAtPosition(int position)
        {
            var users = _inPosition(position);
            var asList = users.Select(x => Program.GetUser(x.Key));
            int count = 0;
            try
            {
                count = users.FirstOrDefault().Value.Count;
            } catch { }
            return new Tuple<List<User>, int>(asList.ToList(), count);
            /*List<User> tied = new List<User>();
            int highest = 0;
            var sorted = this.SortVotes();
            foreach(var u in sorted) //necessary in case there's a tie
            { // could you make a descending list, and stop looping after the num of votes is lower than the first user's (less loops)?
                if(this.Votes[u].Count > highest)
                {
                    highest = this.Votes[u].Count;
                    Program.TryGetUser(u, out User highestU);
                    tied = new List<User>
                    {
                        highestU
                    }; // need to reset
                } else if (this.Votes[u].Count == highest)
                {
                    Program.TryGetUser(u, out User hig);
                    tied.Add(hig);
                }
            }
            if(giveSecondHighest)
            { // we want to return the next highest, which is LOWEST than highest
                int secondHighest = 0;
                tied = new List<User>();
                foreach(var u in sorted)
                {
                    int votes = this.Votes[u].Count;
                    if(votes > secondHighest &&  votes < highest)
                    {
                        secondHighest = votes;
                        Program.TryGetUser(u, out User hig);
                        tied = new List<User>
                        {
                            hig
                        };
                    } else if (votes == secondHighest)
                    {
                        Program.TryGetUser(u, out User user);
                        tied.Add(user);
                    }
                }
                highest = secondHighest;    // for the tuple
            }
            Tuple<List<User>, int> returns = new Tuple<List<User>, int>(tied, highest);
            return returns;*/
        }


        /// <summary>
        /// Returns the two votes made by the inputted user
        /// </summary>
        /// <param name="votingUser">User who you want the votes for</param>
        /// <returns>Two user, or null</returns>
        public Tuple<User, User> GetVotesBy(User votingUser)
        {
            List<User> voted = new List<User>();
            foreach(var vote in Votes)
            {
                if(vote.Value.Contains(votingUser))
                {
                    voted.Add(Program.GetUser(vote.Key));
                }
            }
            // "OrDefault" means it will return null instead of erroring.
            return new Tuple<User, User>(voted.ElementAtOrDefault(0), voted.ElementAtOrDefault(1));
        }


        /// <summary>
        /// Returns a list of the users who voted --for-- the voted
        /// </summary>
        /// <param name="voted">User who was voted for</param>
        public List<User> GetVotesFor(User voted)
        {
            foreach(var vote in Votes)
            {
                if (vote.Key == voted.AccountName)
                    return vote.Value;
            }
            return new List<User>();
        }

        /// <summary>
        /// Adds the vote specified, creating a new Dictionary entry if needed
        /// </summary>
        /// <param name="voted">Who was nominated</param>
        /// <param name="votedBy">Person that was doing the voting.</param>
        public void AddVote(User voted, User votedBy) //add a vote to 'voted'
        {
            if (voted == null)
                return;
            if (voted.AccountName == votedBy.AccountName)
            {
                throw new ArgumentException("Both users are the same object, or share the same name");
            }
            if(Votes.ContainsKey(voted.AccountName)) 
            {
                Votes[voted.AccountName].Add(votedBy); //add a vote to an existing voter
            } else

            {
                Votes.Add(voted.AccountName, new List<User>() { votedBy }); //create a new voter + add their vote
            }
        }
        public override string ToString()
        {
            return $"{ID}: {Votes.Count} {Prompt}";
        }
    }

    /// <summary>
    /// Database manually entered flags
    /// </summary>
    public static class Flags
    {
        /// <summary>
        /// Supresses the warning about account name length being different from 'cheale14'
        /// </summary>
        public const string Ignore_Length = "ignore-length";
        /// <summary>
        /// Allows votes by an account to be displayed, even if they cannot be verfified via IP.
        /// </summary>
        public const string View_Online = "view-online";
        /// <summary>
        /// Overrides the above, and prevents the above from happening
        /// </summary>
        public const string Disallow_View_Online = "disallow-view-online";
        /// <summary>
        /// Indicates the person is a non-Students
        /// </summary>
        public const string Coundon_Staff = "cc-staff";
        /// <summary>
        /// Indicates that the person should not be permitted to actually vote
        /// </summary>
        public const string Disallow_Vote_Staff = "block-vote";

        /// <summary>
        /// Upon connection, makes the user a system operator.
        /// </summary>
        public const string System_Operator = "sysop";
    }

    public class UserVoteSubmit
    {
        static object lockObj = new object();
        public User VotingFor;
        public Dictionary<int, Tuple<User, User>> Votes = new Dictionary<int, Tuple<User, User>>();
        public void AddVote(int cat, User u1 = null, User u2 = null)
        {
            if (Votes.ContainsKey(cat))
                Votes[cat] = new Tuple<User, User>(u1, u2);
            else
                Votes.Add(cat, new Tuple<User, User>(u1, u2));
        }
        public void AddVote(Category cat, User u1 = null, User u2 = null) => AddVote(cat.ID, u1, u2);
        public UserVoteSubmit(User _for)
        {
            VotingFor = _for;
        }
        public string ConfirmString
        {
            get
            {
                string t = "";
                foreach (var cat in Votes.Values)
                {
                    t += $"{(cat.Item1?.AccountName ?? "")};{cat.Item2?.AccountName ?? ""}#";
                }
                return t;
            }
        }
        public bool Submit()
        {
            bool errored = false;
            lock(lockObj)
            {
                try
                {
                    System.IO.File.AppendAllText(Program.Options.ServerTextFileVotes_Path, $"{VotingFor.AccountName}/{ConfirmString}\n");
                }
                catch (Exception ex)
                {
                    Logging.Log("SubmitIO", ex);
                    errored = true;
                }
            }
            foreach(var vote in Votes)
            {
                if(vote.Value.Item1 != null)
                    Program.Database.AddVoteFor(vote.Key, vote.Value.Item1, this.VotingFor);
                if(vote.Value.Item2 != null)
                    Program.Database.AddVoteFor(vote.Key, vote.Value.Item2, this.VotingFor);
            }
            return errored;
        }
    }

}
