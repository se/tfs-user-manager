using System;
using System.Linq;
using Microsoft.TeamFoundation.Framework.Common;
using Microsoft.TeamFoundation.Client;
using Microsoft.TeamFoundation.Framework.Client;
using System.Collections.Generic;
using Microsoft.TeamFoundation.Server;

namespace TFSUserManager
{
    class Program
    {
        static TfsConfigurationServer _server;
        static List<string> _ignoredCollections = new List<string>();
        static List<string> _users = new List<string>();
        static void Main(string[] args)
        {
            var url = args?.FirstOrDefault();

            if (string.IsNullOrEmpty(url))
            {
                Log("You must send Url as arg.", ConsoleColor.Red);
                Log("Press key to exit.", ConsoleColor.White);
                Console.ReadKey();
                return;
            }

            _server = TfsConfigurationServerFactory.GetConfigurationServer(new Uri(url));
            _server.Connect(ConnectOptions.IncludeServices);

            Log($"Connected to {url}", ConsoleColor.Green);

            _server.GetAuthenticatedIdentity(out var identity);

            Log($"Connected identity is {identity.DisplayName} : {identity.UniqueName}", ConsoleColor.White);

            Init(args);
        }

        private static void Init(string[] args)
        {
            Log("What do you want to do?", ConsoleColor.White);
            Log("[L] : List All Users | [R] : Remove User from All Projects and Collections | [Q] : Quit");
            var key = Console.ReadKey();
            Log(string.Empty);

            if (key.Key == ConsoleKey.Q)
            {
                return;
            }

            if (key.Key == ConsoleKey.L)
            {
                ListUsers(args);
                Init(args);
                return;
            }

            if (key.Key == ConsoleKey.R)
            {
                RemoveUser(args);
                Init(args);
                return;
            }

            Log("Sorry I didn't understand.");
            Init(args);
        }

        private static void ListUsers(string[] args)
        {
            _users.Clear();

            Log("Here your users:", ConsoleColor.White);

            var collectionNodes = _server.CatalogNode.QueryChildren(new[] { CatalogResourceTypes.ProjectCollection }, false, CatalogQueryOptions.None);

            foreach (var collectionNode in collectionNodes)
            {
                var collectionId = new Guid(collectionNode.Resource.Properties["InstanceId"]);
                var collection = _server.GetTeamProjectCollection(collectionId);
                if (_ignoredCollections.Contains(collection.Name))
                {
                    Log($"{collection.Name} ignored.", ConsoleColor.DarkRed);
                    continue;
                }
                else
                {
                    Log($"Collection: {collection.Name}");
                }

                var iservice = collection.GetService<IIdentityManagementService>();

                if (iservice == null)
                {
                    Log("Identity Service not found.", ConsoleColor.Red);
                    Console.ReadKey();
                    return;
                }

                var teamService = _server.GetService<TfsTeamService>();
                if (teamService == null)
                {
                    Log("Team Service not found.", ConsoleColor.Red);
                    Console.ReadKey();
                    return;
                }

                var identity = iservice.ReadIdentity(IdentitySearchFactor.AccountName, "Project Collection Valid Users", MembershipQuery.Expanded, ReadIdentityOptions.None);

                if (identity == null)
                {
                    Log($"Project Collection Valid Users not found for {collection.Name}", ConsoleColor.Red);
                    continue;
                }

                foreach (var member in identity.Members)
                {
                    if (member.IdentityType.Equals("Microsoft.TeamFoundation.UnauthenticatedIdentity") ||
                        member.IdentityType.Equals("Microsoft.TeamFoundation.Identity"))
                    {
                        continue;
                    }

                    var identityDescriptor = new IdentityDescriptor(member.IdentityType, member.Identifier);
                    var identityMember = iservice.ReadIdentity(identityDescriptor, MembershipQuery.None, ReadIdentityOptions.IncludeReadFromSource);

                    if (!_users.Contains(identityMember.UniqueName))
                    {
                        _users.Add(identityMember.UniqueName);
                    }
                }
            }

            Log("Users:");

            _users = _users.OrderBy(a => a).ToList();
            foreach (var item in _users)
            {
                Log($"{item}", ConsoleColor.White);
            }

            Log("Completed.", ConsoleColor.DarkGreen);
        }

        private static void RemoveUser(string[] args)
        {
            Log("How do you want to remove your user?");
            Log("[T] Type , [S] Select, [R] Return back");

            ConsoleKeyInfo key;
            do
            {
                key = Console.ReadKey();
            } while (key.Key != ConsoleKey.T && key.Key != ConsoleKey.S && key.Key != ConsoleKey.R);
            Log(string.Empty);
            if (key.Key == ConsoleKey.R)
            {
                Init(args);
                return;
            }

            var userName = string.Empty;
            if (key.Key == ConsoleKey.T)
            {
                Log("Type User Identity (UserName) you want to remove:", ConsoleColor.White);
                Log("Press enter if you want to exit.", ConsoleColor.DarkGray);
                userName = Console.ReadLine();
            }
            else
            {
                if (_users.Count == 0)
                {
                    Log("There is no users to select.");
                    RemoveUser(args);
                    return;
                }
                Log("You can use Left, Right, Up, Down button on your keyboard.");
                var index = -1;
                ConsoleKeyInfo direction;
                do
                {
                    direction = Console.ReadKey();
                    if (direction.Key == ConsoleKey.Escape)
                    {
                        RemoveUser(args);
                        return;
                    }

                    if (direction.Key == ConsoleKey.UpArrow || direction.Key == ConsoleKey.RightArrow)
                    {
                        index++;
                        if (index > _users.Count - 1)
                        {
                            index = 0;
                        }
                    }
                    else if (direction.Key == ConsoleKey.DownArrow || direction.Key == ConsoleKey.LeftArrow)
                    {
                        index--;
                        if (index < 0)
                        {
                            index = _users.Count - 1;
                        }
                    }

                    userName = _users[index];
                    Log(userName, ConsoleColor.Magenta, true, true);

                } while (direction.Key != ConsoleKey.Enter);
                Log("-----------------------------------", ConsoleColor.White);
            }

            if (string.IsNullOrEmpty(userName))
            {
                return;
            }

            Log($"{userName} will be remove.");

            var collectionNodes = _server.CatalogNode.QueryChildren(new[] { CatalogResourceTypes.ProjectCollection }, false, CatalogQueryOptions.None);

            foreach (var collectionNode in collectionNodes)
            {
                var collectionId = new Guid(collectionNode.Resource.Properties["InstanceId"]);
                var collection = _server.GetTeamProjectCollection(collectionId);
                if (_ignoredCollections.Contains(collection.Name))
                {
                    Log($"{collection.Name} ignored.", ConsoleColor.DarkRed);
                    continue;
                }
                else
                {
                    Log($"Collection: {collection.Name}");
                }

                var iservice = collection.GetService<IIdentityManagementService>();

                if (iservice == null)
                {
                    Log("Identity Service not found.", ConsoleColor.Red);
                    Console.ReadKey();
                    return;
                }

                var teamService = _server.GetService<TfsTeamService>();
                if (teamService == null)
                {
                    Log("Team Service not found.", ConsoleColor.Red);
                    Console.ReadKey();
                    return;
                }

                Log("Reading Identitites", ConsoleColor.White);

                var removedIdentity = iservice.ReadIdentity(IdentitySearchFactor.AccountName, userName, MembershipQuery.Expanded, ReadIdentityOptions.IncludeReadFromSource);

                if (removedIdentity != null)
                {
                    foreach (var member in removedIdentity.MemberOf)
                    {
                        try
                        {
                            var identityDescriptor = new IdentityDescriptor(member.IdentityType, member.Identifier);
                            var groupIdentity = iservice.ReadIdentity(identityDescriptor, MembershipQuery.None, ReadIdentityOptions.IncludeReadFromSource);

                            Log($"{groupIdentity.DisplayName} [{groupIdentity.IsActive}]", ConsoleColor.Gray);

                            iservice.RemoveMemberFromApplicationGroup(groupIdentity.Descriptor, removedIdentity.Descriptor);
                            Log("Removed.");
                        }
                        catch (Exception ex)
                        {
                            Log($"Not removed. Because : {ex.Message}", ConsoleColor.Red);
                        }
                    }
                }
                else
                {
                    Log($"{userName} is not found. It must be removed.", ConsoleColor.Green);
                }
            }

            Log("Completed.", ConsoleColor.DarkGreen);
        }

        private static string _lastMessage;
        private static void Log(string message, ConsoleColor color = ConsoleColor.Yellow, bool singleLine = false, bool currentLine = false)
        {
            var temp = Console.ForegroundColor;
            Console.ForegroundColor = color;
            if (singleLine)
            {
                if (currentLine)
                {
                    if (!string.IsNullOrEmpty(_lastMessage))
                    {
                        Console.SetCursorPosition(0, Console.CursorTop);
                        Console.Write(" ".PadLeft(_lastMessage.Length + 1));
                    }
                    Console.SetCursorPosition(0, Console.CursorTop);
                }
                Console.Write(message, ConsoleColor.Green);
            }
            else
            {
                Console.WriteLine(message, ConsoleColor.Green);
            }
            Console.ForegroundColor = temp;
            _lastMessage = message;
        }
    }
}
