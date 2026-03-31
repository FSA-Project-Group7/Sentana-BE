using Sentana.API.Models;
using Sentana.API.DTOs;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Sentana.API.Services
{
	public interface INewsService
	{
		Task<IEnumerable<News>> GetActiveNewsAsync();
		Task<IEnumerable<News>> GetDeletedNewsAsync();
		Task<News> CreateNewsAsync(NewsDto dto);
		Task<bool> UpdateNewsAsync(int id, NewsDto dto);
		Task<bool> SoftDeleteNewsAsync(int id);
		Task<bool> RestoreNewsAsync(int id);
		Task<bool> HardDeleteNewsAsync(int id);
	}
}