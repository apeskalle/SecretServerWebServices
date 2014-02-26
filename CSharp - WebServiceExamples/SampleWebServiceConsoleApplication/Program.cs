using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Text;
using SampleWebServiceConsoleApplication.SSWebService;

/*
 * This application is a sample Console Application that ties together several operations to allow browsing and creating Secrets.
*/
namespace SampleWebServiceConsoleApplication
{
	class Program
	{
		//Secret Server web service methods use -1 to represent a null numeric value.
		public const int NUMERIC_NULL = -1;

		static void Main()
		{
			var account = ReadAccount();
			var client = GetClient(account);
			var authenticateResult = client.Authenticate(account.UserName, account.Password, string.Empty, account.Domain);
			if (authenticateResult.Errors.Length > 0)
			{
				Console.WriteLine("The following errors occured:");
				Array.ForEach(authenticateResult.Errors, s => Console.WriteLine("\t{0}", s));
				return;
			}
			else
			{
				Console.Clear();
				Console.WriteLine("Authentication successful.");
			}
			MainMenu(account, authenticateResult.Token, new Dictionary<string, Func<Account, string, bool>>
			{
				{"Create a new Secret", CreateNewSecret},
				{"Show a Favorite", DisplayFavorites},
				{"Browse folders", BrowseFolders},
				{"Quit", (a, t) => false}
			});
		}

		/// <summary>
		/// Creates a WCF end point for communicating the Secret Server's webservices
		/// </summary>
		private static SSWebServiceSoapClient GetClient(Account account)
		{
			var binding = new BasicHttpBinding();
			//Create an endpoint for the URI.
			var endpoint = new EndpointAddress(account.Url);
			var client = new SSWebServiceSoapClient(binding, endpoint);
			return client;
		}

		private static bool BrowseFolders(Account account, string token)
		{
			var client = GetClient(account);
			Action<int, int, SSWebServiceSoapClient> printFolders = null;
			printFolders = (folderId, indentLevel, soapClient) =>
			{
				var indentation = new string('-', indentLevel * 3);
				var result = soapClient.FolderGetAllChildren(token, folderId);
				foreach (var folder in result.Folders)
				{
					Console.Write(indentation);
					Console.WriteLine(folder.Name);
					printFolders(folder.Id, indentLevel + 1, soapClient);
				}
			};
			printFolders(NUMERIC_NULL, 0, client);
			return true;
		}

		private static bool CreateNewSecret(Account account, string token)
		{
			var soapClient = GetClient(account);
			Console.WriteLine("Please choose a type: ");
			var templates = soapClient.GetSecretTemplates(token);
			var template = GenericMenu(templates.SecretTemplates.ToDictionary(k => k.Name));
			if (template == null)
			{
				return true;
			}
			Console.Write("Please enter a name: ");
			var secretName = Console.ReadLine();
			var fieldValues = new Dictionary<int, string>();
			foreach (var field in template.Fields)
			{
				Console.Write("Value for {0}: ", field.DisplayName);
				var value = Console.ReadLine();
				fieldValues.Add(field.Id, value);
			}
			var result = soapClient.AddSecret(token, template.Id, secretName, fieldValues.Keys.ToArray(), fieldValues.Values.ToArray(), NUMERIC_NULL);
			if (result.Errors.Length == 0)
			{
				Console.WriteLine("Secret added successfully.");
			}
			else
			{
				Console.WriteLine("There was an error adding your secret.");
			}
			return true;
		}

		private static bool DisplayFavorites(Account account, string token)
		{
			var soapClient = GetClient(account);
			var favorites = soapClient.GetFavorites(token, false);
			var selection = GenericMenu(favorites.SecretSummaries.ToDictionary(k => k.SecretName));
			if (selection == null)
			{
				return true;
			}
			DisplaySecret(selection, account, token);
			return true;
		}

		private static Secret GetSecret(int secretId, Account account, string token)
		{
			var soapClient = GetClient(account);
			return soapClient.GetSecret(token, secretId, null, null).Secret;
		}

		private static void DisplaySecret(SecretSummary secretSummary, Account account, string token)
		{
			Console.WriteLine();
			Console.WriteLine(secretSummary.SecretName);
			Console.WriteLine();
			var secret = GetSecret(secretSummary.SecretId, account, token);
			foreach (var item in secret.Items)
			{
				Console.WriteLine("{0}: {1}", item.FieldDisplayName, item.Value);
			}
		}

		private static Account ReadAccount()
		{
			Console.Write("Please enter your Secret Server URL: ");
			var url = Console.ReadLine();
			Console.Write("Please enter your user name: ");
			var username = Console.ReadLine();
			Console.Write("Please enter your password: ");
			var passwordBuilder = new StringBuilder();
			while (true)
			{
				var key = Console.ReadKey(true);
				if (key.Key == ConsoleKey.Enter)
				{
					Console.WriteLine();
					break;
				}
				if (key.Key == ConsoleKey.Backspace)
				{
					if (passwordBuilder.Length == 0)
					{
						continue;
					}
					Console.SetCursorPosition(Console.CursorLeft - 1, Console.CursorTop);
					Console.Write(' ');
					Console.SetCursorPosition(Console.CursorLeft - 1, Console.CursorTop);
					passwordBuilder.Remove(passwordBuilder.Length - 1, 1);
				}
				else if (!char.IsControl(key.KeyChar))
				{
					passwordBuilder.Append(key.KeyChar);
					Console.Write('*');
				}
			}
			Console.Write("Please enter your domain (press enter if no domain): ");
			var domain = Console.ReadLine();
			return new Account
			       	{
			       		Domain = domain,
			       		Password = passwordBuilder.ToString(),
			       		UserName = username,
						Url = url
			       	};
		}

		private static T GenericMenu<T>(Dictionary<string, T> items) where T:class
		{
			Console.WriteLine();
			var options = "123456789ACBDEFGHIJKLMNOPQRSTUVWXYZ".ToCharArray();
			Console.WriteLine("Please choose:");
			var i = 0;
			foreach (var item in items)
			{
				Console.WriteLine("\t{0}: {1}", options[i++], item.Key);
			}
			Console.WriteLine("\tB: Back");
			while (true)
			{
				var key = Console.ReadKey(true);
				var keyChar = key.KeyChar;
				if (char.IsDigit(keyChar) || char.IsLetter(keyChar))
				{
					if (char.ToUpperInvariant(keyChar) == 'B')
					{
						return null;
					}
					Console.WriteLine();
					var index = Array.FindIndex(options, p => Char.ToLower(p) == Char.ToLower(keyChar));
					if (index >= 0 && index < i)
					{
						return items.ElementAt(index).Value;
					}
				}
			}
		}

		private static void MainMenu(Account account, string token, Dictionary<string, Func<Account, string, bool>> items)
		{
			Console.WriteLine();
			var options = "123456789ACBDEFGHIJKLMNOPQRSTUVWXYZ".ToCharArray();
			Console.WriteLine("Please choose:");
			var i = 0;
			foreach (var item in items)
			{
				Console.WriteLine("\t{0}: {1}", options[i++], item.Key);
			}
			while (true)
			{
				var key = Console.ReadKey(true);
				var keyChar = key.KeyChar;
				if (char.IsDigit(keyChar) || char.IsLetter(keyChar))
				{
					var index = Array.FindIndex(options, p => Char.ToLower(p) == Char.ToLower(keyChar));
					if (index >= 0 && index < i)
					{
						if (items.ElementAt(index).Value(account, token))
						{
							MainMenu(account, token, items);
						}
						else
						{
							break;
						}
					}
				}
			}
		}
	}
}
