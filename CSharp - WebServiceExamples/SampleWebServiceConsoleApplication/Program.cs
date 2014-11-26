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
            //var account = new Account() { Domain = "myDomain", Password = "myPassword", Url = "http://localhost/SecretServer/webservices/SSWebservice.asmx", UserName = "myUsername" };            
			var client = GetClient(account);
			var authenticateResult = client.Authenticate(account.UserName, account.Password, string.Empty, account.Domain);
			if (authenticateResult.Errors.Length > 0)
			{
				Console.WriteLine("The following errors occured:");
				Array.ForEach(authenticateResult.Errors, s => Console.WriteLine("\t{0}", s));
				Console.ReadLine();
				return;
			}
			else
			{
				Console.Clear();
				Console.WriteLine("Authentication successful.");
			}
			MainMenu(account, authenticateResult.Token, new Dictionary<string, Func<Account, string, bool>>
			{
				{"Create a new Secret (Advanced)", CreateNewSecretAdvanced},
				{"Show a Favorite", DisplayFavorites},
				{"Browse folders", BrowseFolders},
				{"Search Secret", SearchSecret},
				{"Update Secret Permissions", UpdateSecretPermissions},
				{"Edit Secret", EditSecret},
				{"Delete Secret", DeleteSecret},
                {"CheckIn Secret", CheckInSecret},
				{"Create a new Secret (Legacy)", CreateNewSecret},
				{"Add Custom Secret Audit", AddCustomSecretAudit},
				{"Terminate Session", TerminateSession},
                {"Search Secret By Field Value", SearchSecretByFieldValue},
                {"Get Blank Secret", GetNewSecret},
				{"Quit", (a, t) => false}
			});
		}

	    private static Folder GetFolder(Account account, string token)
        {
            var soapClient = GetClient(account);
            Console.Write("Please enter a folder name to search: ");
            var folderName = Console.ReadLine();


            var folderResult = soapClient.SearchFolders(token, folderName);
            if (folderResult.Errors.Length > 0)
            {
                Console.WriteLine("Folder Search Error." + string.Join(",", folderResult.Errors));
                return null;
            }

            var folders = folderResult.Folders.ToList();
            var rootFolder = new Folder() { Id = -1, Name = "(root)", ParentFolderId = -1 };
            folders.Add(rootFolder);
            var folder = GenericMenu(folders.ToDictionary(k => k.Name + " (" + k.Id + ")"));
            return folder;
        }

        private static bool CreateNewSecretAdvanced(Account account, string token)
	    {
            var soapClient = GetClient(account);
            Console.WriteLine("Please choose a type: ");
            var templates = soapClient.GetSecretTemplates(token);
            var template = GenericMenu(templates.SecretTemplates.ToDictionary(k => k.Name));
            if (template == null)
            {
                return true;
            }

            var folder = GetFolder(account, token);
            var getSecretResult = soapClient.GetNewSecret(token, template.Id, folder.Id);
            if (!HandleSecretResult(getSecretResult))
            {
                return true;
            }
            Secret newSecret = getSecretResult.Secret;

            Console.Write("Please enter a Secret name: ");
            var secretName = Console.ReadLine();
            newSecret.Name = secretName;
            ChangeItemValues(newSecret);

            while (true)
            {
                if (EditSecretInternal(newSecret, account, token, soapClient))
                {
                    return true;
                }
                
                var result = soapClient.AddNewSecret(token, newSecret);
                if (result.Errors.Length == 0)
                {
                    Console.WriteLine("Secret added successfully.");
                    DisplaySecret(result.Secret);
                    return true;
                }
                else
                {
                    Console.WriteLine("There was an error adding your secret. " + string.Join(",", result.Errors));
                }
            }
	    }

        private static bool GetNewSecret(Account account, string token)
        {
            var soapClient = GetClient(account);
            Console.WriteLine("Please choose a type: ");
            var templates = soapClient.GetSecretTemplates(token);
            var template = GenericMenu(templates.SecretTemplates.ToDictionary(k => k.Name));
            if (template == null)
            {
                return true;
            }

            var folder = GetFolder(account, token);
            var getSecretResult = soapClient.GetNewSecret(token, template.Id, folder.Id);
            if (!HandleSecretResult(getSecretResult))
            {
                return true;
            }

            if (getSecretResult.Errors.Length == 0)
            {
                Console.WriteLine("Secret added successfully.");
                DisplaySecret(getSecretResult.Secret);
                return true;
            }

            return true;

        }

	    private static void ChangeItemValues(Secret newSecret)
	    {
	        foreach (var item in newSecret.Items)
	        {
	            Console.Write("Value for {0}: ", item.FieldDisplayName);
	            var value = Console.ReadLine();
	            item.Value = value;
	        }
	    }

        private static void ChangePermissions(bool hasFolder, Secret secret)
	    {
	        bool hasPermissionChange = false;
	        if (hasFolder)
	        {
	            var inherit = GetYesOrNoAnswer("Inherit Permissions Enabled");
                secret.SecretPermissions.InheritPermissionsEnabled = inherit;
                hasPermissionChange = true;
	        }
	        if (!hasFolder || !secret.SecretPermissions.InheritPermissionsEnabled.GetValueOrDefault())
	        {
                List<string> permissionOptions = new List<string>();
                var permissions = new List<Permission>();

	            foreach (var permissionOption in secret.SecretPermissions.Permissions)
	            {
	                permissionOptions.Add(permissionOption.UserOrGroup.Name);
                    permissions.Add(permissionOption);
	            }
                permissionOptions.Add("Add Permission");
                permissionOptions.Add("Done");

	            while (true)
	            {
	                var chooseMenu = GetChooseMenu(permissionOptions.ToArray());
	                if (chooseMenu == null || chooseMenu == "Done")
	                {
	                    break;
	                }
	                if (chooseMenu == "Add Permission")
	                {
                        permissions.Add(AddPermission());
	                }
                    else
                    {
                        var existing = permissions.First(p => p.UserOrGroup.Name == chooseMenu);
                        permissions.Remove(existing);
                        var permission = SetPermissionForUserOrGroup(existing.UserOrGroup);
                        if (permission != null)
                        {
                            permissions.Add(permission);                            
                        }
                    }
	                //Reset Choices
                    permissionOptions = new List<string>();
                    foreach (var permissionOption in permissions)
                    {
                        permissionOptions.Add(permissionOption.UserOrGroup.Name);
                    }
                    permissionOptions.Add("Add Permission");
                    permissionOptions.Add("Done");
	            }
	            
	            secret.SecretPermissions.Permissions = permissions.ToArray();

	            hasPermissionChange = true;
	        }
	        secret.SecretPermissions.IsChangeToPermissions = hasPermissionChange;
	    }

	    private static void ChangeSettings(Secret secret, string token, SSWebServiceSoapClient soapClient)
	    {
	        secret.SecretSettings.AutoChangeEnabled = GetYesOrNoAnswer("AutoChange Enabled");
	        secret.SecretSettings.RequiresComment = GetYesOrNoAnswer("Require Comment");
	        if (GetYesOrNoAnswer("Require Approval For Access"))
	        {
	            secret.SecretSettings.RequiresApprovalForAccess = true;
	            var groupOrUserRecord = GetGroupOrUserRecord();
	            var approvers = new List<GroupOrUserRecord>();
                approvers.Add(groupOrUserRecord);
	            secret.SecretSettings.Approvers = approvers.ToArray();
	        }
	        else
	        {
                secret.SecretSettings.RequiresApprovalForAccess = false;
	        }
            if (GetYesOrNoAnswer("Use Privilege Account"))
	        {
	            var secretSummary = GetSearchSecretSummary(token, soapClient, true);
	            if (secretSummary == null)
	            {
                    secret.SecretSettings.PrivilegedSecretId = null;
	            }
	            else
	            {
	                secret.SecretSettings.PrivilegedSecretId = secretSummary.SecretId;
	            }
	        }
            else
            {
                secret.SecretSettings.PrivilegedSecretId = null;
            }
            secret.SecretSettings.CheckOutEnabled = GetYesOrNoAnswer("CheckOut Enabled");
	        if (secret.SecretSettings.CheckOutEnabled.GetValueOrDefault())
	        {
                secret.SecretSettings.CheckOutChangePasswordEnabled = GetYesOrNoAnswer("CheckOut Change Password Enabled");
	        }
	        secret.SecretSettings.IsChangeToSettings = true;
	    }

	    private static Permission AddPermission()
	    {
	        var groupOrUserRecord = GetGroupOrUserRecord();
	        return SetPermissionForUserOrGroup(groupOrUserRecord);
	    }

	    private static Permission SetPermissionForUserOrGroup(GroupOrUserRecord groupOrUserRecord, bool returnRemove = false)
	    {
	        var permission = new Permission();
	        permission.UserOrGroup = groupOrUserRecord;

	        var chooseMenu = GetChooseMenu("Owner", "Edit", "View", "Remove");
            if (chooseMenu == null)
	        {
	            return permission;
	        }
	        if (chooseMenu == "Remove")
	        {
                return returnRemove ? permission : null;
	        }

	        permission.Owner = chooseMenu == "Owner";
            permission.Edit = permission.Owner || chooseMenu == "Edit";
            permission.View = permission.Edit || chooseMenu == "View";
	        return permission;
	    }

	    private static bool GetYesOrNoAnswer(string message)
        {
            Console.Write("{0} (Y-N): ", message);
            var value = Console.ReadLine();
            return value.ToUpper().StartsWith("Y");
        }

	    private static GroupOrUserRecord GetGroupOrUserRecord()
	    {
	        var groupOrUserRecord = new GroupOrUserRecord();
	        Console.Write("Adding User(U) or Group(G)?: ");
	        var gOrU = Console.ReadLine();
	        groupOrUserRecord.IsUser = gOrU.ToUpper().StartsWith("U");

	        Console.Write("Enter User Or Group Name: ");
	        var name = Console.ReadLine();
	        groupOrUserRecord.Name = name;
	        return groupOrUserRecord;
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

            var folder = GetFolder(account, token);
		    int folderId = NUMERIC_NULL;
            if (folder != null)
            {
                folderId = folder.Id;
            }

            var result = soapClient.AddSecret(token, template.Id, secretName, fieldValues.Keys.ToArray(), fieldValues.Values.ToArray(), folderId);
			if (result.Errors.Length == 0)
			{
				Console.WriteLine("Secret added successfully.");
			}
			else
			{
				Console.WriteLine("There was an error adding your secret." + string.Join(",", result.Errors));
			    return true;
			}
		    var secretResult = GetSecret(result.Secret.Id, account, token);
		    if (!HandleSecretResult(secretResult))
		    {
		        return true;
		    }
            DisplaySecret(secretResult.Secret);
			return true;
		}

        private static bool SearchSecretByFieldValue(Account account, string token)
        {
            var soapClient = GetClient(account);
            var secretSummary = GetSearchSecretsByFieldValue(token, soapClient);
            if (secretSummary == null)
            {
                return true;
            }
            return true;
        }

	    private static bool SearchSecret(Account account, string token)
	    {
            var soapClient = GetClient(account);
	        var secretSummary = GetSearchSecretSummary(token, soapClient);
	        if (secretSummary == null)
            {
                return true;
            }
            var secretResult = GetSecret(secretSummary.SecretId, account, token);
	        if (!HandleSecretResult(secretResult))
	        {
	            return true;
	        }
            DisplaySecret(secretResult.Secret);
            return true;
	    }

        private static List<Secret> GetSearchSecretsByFieldValue(string token, SSWebServiceSoapClient soapClient)
        {
            Console.WriteLine("Enter Field Name: ");
            var fieldName = Console.ReadLine();
            Console.WriteLine("Enter Search Term: ");
            var searchTerm = Console.ReadLine();
            var secrets = soapClient.GetSecretsByFieldValue(token, fieldName, searchTerm, false);
            if (secrets.Errors.Length != 0)
            {
                Console.WriteLine("Error: " + string.Join(",", secrets.Errors));
                return null;
            }
            if (secrets.Secrets.Length == 0)
            {
                Console.WriteLine("No Secrets were found.");
                return null;
            }

            foreach (var secret in secrets.Secrets)
            {
                DisplaySecret(secret);
            }

            return secrets.Secrets.ToList();
        }

	    private static SecretSummary GetSearchSecretSummary(string token, SSWebServiceSoapClient soapClient, bool includeRestricted = true)
	    {
	        Console.WriteLine("Enter Search Term: ");
	        var secretName = Console.ReadLine();
	        var secrets = soapClient.SearchSecrets(token, secretName, false, includeRestricted);
	        if (secrets.Errors.Length != 0)
	        {
	            Console.WriteLine("Error: " + string.Join(",", secrets.Errors));
	            return null;
	        }
	        if (secrets.SecretSummaries.Length == 0)
	        {
	            Console.WriteLine("No Secrets were found.");
	            return null;
	        }
	        var secretSummary = GenericMenu(secrets.SecretSummaries.ToDictionary(k => k.SecretName + " (" + k.SecretId + ")"));
	        return secretSummary;
	    }

	    private static bool UpdateSecretPermissions(Account account, string token)
	    {
            var soapClient = GetClient(account);
	        var secretSummary = GetSearchSecretSummary(token, soapClient, false);
            if (secretSummary == null)
            {
                return true;
            }
	        bool reloadDisplay = true;
	        while (true)
	        {
	            var secretResult = GetSecret(secretSummary.SecretId, account, token);
	            if (!HandleSecretResult(secretResult))
	            {
	                return true;
	            }
	            var secret = secretResult.Secret;
                if (reloadDisplay)
	            {
                    DisplaySecret(secret);	                
	            }

	            List<string> permissionOptions = new List<string>();
	            var permissions = new List<Permission>();

	            foreach (var permissionOption in secret.SecretPermissions.Permissions)
	            {
	                permissionOptions.Add(permissionOption.UserOrGroup.Name);
	                permissions.Add(permissionOption);
	            }
	            permissionOptions.Add("Add Permission");
                permissionOptions.Add("Done");

	            var chooseMenu = GetChooseMenu(permissionOptions.ToArray());
	            if (chooseMenu == null || chooseMenu == "Done")
	            {
	                break;
	            }
	            Permission permissionToChange = null;
	            if (chooseMenu == "Add Permission")
	            {
	                permissionToChange = AddPermission();

	            }
                else
	            {
	                var existing = permissions.First(p => p.UserOrGroup.Name == chooseMenu);
	                permissions.Remove(existing);
	                permissionToChange = SetPermissionForUserOrGroup(existing.UserOrGroup, true);
	            }
	            permissions.Add(permissionToChange);

	            var updateResult = soapClient.UpdateSecretPermission(token, secretSummary.SecretId, permissionToChange.UserOrGroup, permissionToChange.View, permissionToChange.Edit, permissionToChange.Owner);
	            if (updateResult.Errors.Length > 0)
	            {
	                Console.WriteLine("Error: " + string.Join(",", updateResult.Errors));
	                reloadDisplay = false;
	            }
	            else
	            {
                    reloadDisplay = true;
                    Console.WriteLine("Secret permissions were updated.");
	            }

	        }
	        return true;
	    }

	    private static bool DeleteSecret(Account account, string token)
	    {
	        var soapClient = GetClient(account);
	        var secretSummary = GetSearchSecretSummary(token, soapClient, true);
	        if (secretSummary == null)
	        {
	            return true;
	        }
	        var result = soapClient.DeactivateSecret(token, secretSummary.SecretId);
            if (result.Errors.Length > 0)
            {
                Console.WriteLine("Error: " + string.Join(",", result.Errors));
                return true;
            }
            Console.WriteLine("Secret was deleted.");
	        return true;
	    }

        private static bool CheckInSecret(Account account, string token)
        {
            var soapClient = GetClient(account);
            var secretSummary = GetSearchSecretSummary(token, soapClient, true);
            if (secretSummary == null)
            {
                return true;
			}
            var result = soapClient.CheckIn(token, secretSummary.SecretId);
            if (result.Errors.Length > 0)
            {
                Console.WriteLine("Error: " + string.Join(",", result.Errors));
                return true;
            }
            Console.WriteLine("Secret was checked in.");
            return true;
        }

	    private static bool EditSecret(Account account, string token)
        {
            var soapClient = GetClient(account);
            var secretSummary = GetSearchSecretSummary(token, soapClient, true);
            if (secretSummary == null)
            {
                return true;
            }
            var secretResultBefore = GetSecret(secretSummary.SecretId, account, token);
            if (!HandleSecretResult(secretResultBefore))
            {
                return true;
            }
            var secret = secretResultBefore.Secret;
            DisplaySecret(secret);

	        while (true)
	        {
                if (EditSecretInternal(secret, account, token, soapClient))
                {
                    break;
                }

	            var secretResult = soapClient.UpdateSecret(token, secret);
	            if (secretResult.Errors.Length > 0)
	            {
	                Console.WriteLine("Error: " + string.Join(",", secretResult.Errors));
	            }
	            else
	            {
                    Console.WriteLine("Updated Secret successfully.");
                    break;
	            }
	        }

	        var secretResultAfter = GetSecret(secretSummary.SecretId, account, token);
            if (!HandleSecretResult(secretResultAfter))
            {
			return true;
		}
            DisplaySecret(secretResultAfter.Secret);
            return true;
        }

	    private static bool EditSecretInternal(Secret secret, Account account, string token, SSWebServiceSoapClient soapClient)
	    {
	        var editValues = "Edit Values";
	        var editSettings = "Edit Settings";
	        var editPermissions = "Edit Permissions";
	        var moveFolder = "Move Folder";
	        var done = "Done";

	        while (true)
	        {
	            var menu = GetChooseMenu(editValues, editSettings, editPermissions,moveFolder, done);
	            if (menu == null)
	            {
	                return true;
	            }
	            if (menu == done)
	            {
	                break;
	            }
	            var isValueChange = menu == editValues;
	            if (isValueChange)
	            {
	                ChangeItemValues(secret);
	            }
	            else if (menu == editSettings)
	            {
                    ChangeSettings(secret, token, soapClient);
	            }
	            else if (menu == editPermissions)
	            {
	                ChangePermissions(secret.FolderId > 0, secret);
	            }
                else if (menu == moveFolder)
	            {
	                var folder = GetFolder(account, token);
	                if (folder != null)
	                {
	                    secret.FolderId = folder.Id;
	                }
	            }
	        }
	        return false;
	    }

	    private static string GetChooseMenu(params string [] options)
	    {
	        var dictionary = new Dictionary<string, string>();
	        foreach (var option in options)
	        {
	            dictionary.Add(option, option);
	        }
            var menu = GenericMenu(dictionary);
	        return menu;
	    }

	    private static bool HandleSecretResult(GetSecretResult secretResult)
	    {
	        var hasError = secretResult.Errors.Length > 0;
            if (hasError)
	        {
                Console.WriteLine("Error: " + string.Join(",", secretResult.Errors));
                return false;
	        }
	        return true;
	    }

	    private static void DisplaySecret(Secret secret)
        {
            Console.WriteLine("--------------------------------------");
            Console.Write("Secret Name: ");
            Console.WriteLine(secret.Name);
            Console.WriteLine("--------------------------------------");

	        var indent = "   ";
	        Console.Write(indent);
            Console.Write("Secret Id: ");
            Console.WriteLine(secret.Id);

            Console.Write(indent);
            Console.Write("Folder Id: ");
            Console.WriteLine(secret.FolderId < 1 ? "(root)" : secret.FolderId.ToString());

            foreach (var item in secret.Items)
            {
                Console.Write(indent);
                Console.Write((item.FieldDisplayName ?? item.FieldName) + ": ");
                Console.WriteLine(item.Value);
            }

            var subIndent = indent + "   ";           
	        SecretSettings settings = secret.SecretSettings;
            if (settings != null)
            {
                Console.Write(indent);
                Console.WriteLine("-Settings-");
                WriteSetting(subIndent, "AutoChange Enabled", settings.AutoChangeEnabled);
                WriteSetting(subIndent, "Requires Comment", settings.RequiresComment);
                WriteSetting(subIndent, "Requires Approval for Access", settings.RequiresApprovalForAccess);
                if (settings.RequiresApprovalForAccess.GetValueOrDefault())
                {
                    var approverInString = String.Join(",", settings.Approvers.Select(a => a.Name));
                    WriteSetting(subIndent, "Approvers", approverInString);
                }
                WriteSetting(subIndent, "CheckOut Enabled", settings.CheckOutEnabled);
                WriteSetting(subIndent, "CheckOut Change Password Enabled", settings.CheckOutChangePasswordEnabled);
                WriteSetting(subIndent, "Privilege Account", settings.PrivilegedSecretId);
                WriteSetting(subIndent, "Associated Account", settings.AssociatedSecretIds == null ? "" : string.Join(", ", settings.AssociatedSecretIds));
            }
            SecretPermissions secretPermissions = secret.SecretPermissions;
            if (secretPermissions != null)
            {
                Console.Write(indent);
                Console.WriteLine("-Secret Permissions-");
                WriteSetting(subIndent, "InheritPermissions Enabled", secretPermissions.InheritPermissionsEnabled);
                foreach (var permission in secretPermissions.Permissions)
                {
                    Console.Write(subIndent);
                    Console.Write(permission.UserOrGroup.Name + ": ");
                    if (permission.View)
                    {
                        Console.Write("V"); 
                    }
                    if (permission.Edit)
                    {
                        Console.Write("E"); 
                    }
                    if (permission.Owner)
                    {
                        Console.Write("O"); 
                    }
                    Console.WriteLine("");
                }
            }
            Console.WriteLine("--------------------------------------");
        }

	    private static void WriteSetting(string indent, string name, object settingValue)
	    {
	        Console.Write(indent);
	        Console.Write(name + ": ");
	        Console.WriteLine(settingValue);
	    }

		private static bool DisplayFavorites(Account account, string token)
		{
			var soapClient = GetClient(account);
			var favorites = soapClient.GetFavorites(token, true);
			var selection = GenericMenu(favorites.SecretSummaries.ToDictionary(k => k.SecretName));
			if (selection == null)
			{
				return true;
			}
			DisplaySecret(selection, account, token);
			return true;
		}

	    private static GetSecretResult GetSecretAdvanced(int secretId, Account account, string token, CodeResponse[] codeResponses)
		{
			var soapClient = GetClient(account);
		    var getSecretResult = soapClient.GetSecret(token, secretId, true, codeResponses);
		    if (getSecretResult.Errors.Length == 0)
		    {
                return getSecretResult;		        
		    }
	        var secretError = getSecretResult.SecretError;
	        if (secretError != null && secretError.AllowsResponse)
		    {
                Console.WriteLine(secretError.ErrorMessage);
		        string comment = "";
		        string addtionalComment = "";
		        if (!string.IsNullOrEmpty(secretError.CommentTitle))
		        {
                    Console.Write(secretError.CommentTitle + ":");
		            comment = Console.ReadLine();
		        }
                if (!string.IsNullOrEmpty(secretError.AdditionalCommentTitle))
                {
                    Console.Write(secretError.AdditionalCommentTitle + ":");
                    addtionalComment = Console.ReadLine();
                }
                var codeResponse = new CodeResponse() { ErrorCode = secretError.ErrorCode, Comment = comment, AdditionalComment = addtionalComment};
		        return GetSecretAdvanced(secretId, account, token, new CodeResponse[] {codeResponse});
		    }
	        return getSecretResult;
	    }

	    private static GetSecretResult GetSecret(int secretId, Account account, string token)
		{
			return GetSecretAdvanced(secretId, account, token, new CodeResponse[0]);
		}

		private static void DisplaySecret(SecretSummary secretSummary, Account account, string token)
		{
			Console.WriteLine();
			Console.WriteLine(secretSummary.SecretName);
			Console.WriteLine();
			var secretResult = GetSecret(secretSummary.SecretId, account, token);
		    if (!HandleSecretResult(secretResult))
		    {
		        return;
		    }
		    Secret secret = secretResult.Secret;
			foreach (var item in secret.Items)
			{
				Console.WriteLine("{0}: {1}", item.FieldDisplayName, item.Value);
			}
		}

		private static Account ReadAccount()
		{
			Console.Write("Please enter your Secret Server URL: ");
			var url = Console.ReadLine();
            if (!url.EndsWith("webservices/SSWebservice.asmx"))
            {
                url += "webservices/SSWebservice.asmx";
            }
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

	    private static bool TerminateSession(Account account, string token)
	    {
            var soapClient = GetClient(account);
            Console.WriteLine("Enter SessionKey (passed by custom launcher):");
            string sessionKey = Console.ReadLine();
	        var result = soapClient.CheckInByKey(sessionKey);
	        if (result.Errors.Length > 0)
            {
                Console.WriteLine("Error: " + string.Join(",", result.Errors));
                return true;
            }
	        else
	        {
                Console.WriteLine("Session was successfully terminated. ");
	        }
            return true;
	    }

	    private static bool AddCustomSecretAudit(Account account, string token)
        {
            var soapClient = GetClient(account);

            Console.WriteLine("Enter Secret Id:");
            int secretId = Convert.ToInt32(Console.ReadLine());

            Console.WriteLine("Enter Notes:*");
            string notes = Console.ReadLine();

            Console.WriteLine("Enter User Id:");
            int userId = 0;
            string userIdString = Console.ReadLine();
            if (string.IsNullOrEmpty(userIdString) || !int.TryParse(userIdString, out userId))
            {
                Console.WriteLine("User Id is invalid.");
                return true;
            }

            Console.WriteLine("Enter IP Address:");
            string ipAddress = Console.ReadLine(); 
            
            Console.WriteLine("Enter Reference Id:");
            string referenceIdString = Console.ReadLine();

            int referenceId1;
            int? referenceId;
            if (string.IsNullOrEmpty(referenceIdString) || !int.TryParse(referenceIdString, out referenceId1))
            {
                referenceId = null;
            }
            else
            {
                referenceId = referenceId1;
            }

            Console.WriteLine("Enter Ticket Number:");
            string ticketNumber = Console.ReadLine();  

            var result = soapClient.AddSecretCustomAudit(token, secretId, notes, ipAddress, referenceId, ticketNumber, userId);
            if (result.Errors.Length > 0)
            {
                Console.WriteLine("Error: " + string.Join(",", result.Errors));
                return true;
            }
            Console.WriteLine("Audit Added Successfully.");
            return true;
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
