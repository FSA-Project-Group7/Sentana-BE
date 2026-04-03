using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace Sentana.API.Helpers 
{
	public class CustomUserIdProvider : IUserIdProvider
	{
		public string? GetUserId(HubConnectionContext connection)
		{
			return connection.User?.FindFirst("AccountId")?.Value;
		}
	}
}