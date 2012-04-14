using System;
using System.ServiceModel;
using System.ServiceModel.Channels;
using WebServiceExamples.ServiceReference;

namespace WebServiceExamples
{
	public class SampleWebServices
	{
		/// <summary>
		/// Demonstrates how to authenticate using web services.
		/// Assumes that the account is a local account not using Secret Server Online.
		/// </summary>
		public void SampleAuthenticateInstalledNoDomain()
		{
			//Use a basic HTTP binding for SOAP.
			var binding = new BasicHttpBinding();
			//Create an endpoint for the URI.
			var endpoint = new EndpointAddress("http://yoursecretserver/webservices/sswebservice.asmx");
			var soapClient = new SSWebServiceSoapClient(binding, endpoint);
			var result = soapClient.Authenticate("theUserName", "thePassword", string.Empty, string.Empty);
			if (result.Errors.Length > 0)
			{
				//Authentication failed. The Errors array contains the reason(s).				
			}
			//Successful
			else
			{
				var token = result.Token;
				//token is the authenticate token.
			}
		}

		/// <summary>
		/// Demonstrates how to authenticate using web services.
		/// Assumes that the account is a domain account on domain "example".
		/// </summary>
		public void SampleAuthenticateInstalledWithDomain()
		{
			//Use a basic HTTP binding for SOAP.
			var binding = new BasicHttpBinding();
			//Create an endpoint for the URI.
			var endpoint = new EndpointAddress("http://yoursecretserver/webservices/sswebservice.asmx");
			var soapClient = new SSWebServiceSoapClient(binding, endpoint);
			var result = soapClient.Authenticate("theUserName", "thePassword", string.Empty, "example");
			if (result.Errors.Length > 0)
			{
				//Authentication failed. The Errors array contains the reason(s).				
			}
			//Successful
			else
			{
				var token = result.Token;
				//token is the authenticate token.
			}
		}

		/// <summary>
		/// Example on how to validate a token
		/// </summary>
		public void ValidateTokenIsStillValid()
		{
			//Use a basic HTTP binding for SOAP.
			var binding = new BasicHttpBinding();
			//Create an endpoint for the URI.
			var endpoint = new EndpointAddress("http://yoursecretserver/webservices/sswebservice.asmx");
			var soapClient = new SSWebServiceSoapClient(binding, endpoint);
			var authenticateResult = soapClient.Authenticate("theUserName", "thePassword", string.Empty, string.Empty);
			//Assume authentication was successful.
			var token = authenticateResult.Token;
			var isValidTokenResult = soapClient.GetTokenIsValid(token);
			if (isValidTokenResult.Errors.Length == 0)
			{
				//Token is still valid				
			}
			else
			{
				//Token is invalid. The Errors property on isValidTokenResult contains the reason.
			}
		}

		/// <summary>
		/// Displays a Secret by Secret ID
		/// </summary>
		public void DisplaySecret()
		{
			//Use a basic HTTP binding for SOAP.
			var binding = new BasicHttpBinding();
			//Create an endpoint for the URI.
			var endpoint = new EndpointAddress("http://yoursecretserver/webservices/sswebservice.asmx");
			var soapClient = new SSWebServiceSoapClient(binding, endpoint);
			var result = soapClient.Authenticate("theUserName", "thePassword", string.Empty, string.Empty);
			if (result.Errors.Length > 0)
			{
				//Authentication failed. The Errors array contains the reason(s).				
			}
			//Successful
			else
			{
				var token = result.Token;
				var secretId = 1;
				var getSecretResult = soapClient.GetSecret(token, secretId);
				if (getSecretResult.Errors.Length > 0)
				{
					//Failed to get the secret. The Errors array contains the reason(s).
				}
				else
				{
					//The display name of the Secret
					var secretName = getSecretResult.Secret.Name;
					//The items of the secret
					var items = getSecretResult.Secret.Items;
					foreach (var item in items)
					{
						//The display name of the field.
						var fieldName = item.FieldDisplayName;
						//The value of the field.
						var fieldValue = item.Value;
					}
				}
			}
		}
	}
}
