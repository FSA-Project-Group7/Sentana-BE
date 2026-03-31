using Microsoft.EntityFrameworkCore;
using Sentana.API.Models;
using Sentana.API.DTOs;
using Sentana.API.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Sentana.API.Services
{
	public class NewsService : INewsService
	{
		private readonly SentanaContext _context;

		public NewsService(SentanaContext context)
		{
			_context = context;
		}

		public async Task<IEnumerable<News>> GetActiveNewsAsync()
		{
			return await _context.News
				.Where(n => n.IsDeleted == false || n.IsDeleted == null)
				.OrderByDescending(n => n.CreatedAt)
				.ToListAsync();
		}

		public async Task<IEnumerable<News>> GetDeletedNewsAsync()
		{
			return await _context.News
				.Where(n => n.IsDeleted == true)
				.OrderByDescending(n => n.CreatedAt)
				.ToListAsync();
		}

		public async Task<News> CreateNewsAsync(NewsDto dto)
		{
			var news = new News
			{
				Title = dto.Title?.Trim(),
				Description = dto.Description?.Trim(),
				CreatedAt = DateTime.Now,
				CreateDay = DateOnly.FromDateTime(DateTime.Now),
				IsDeleted = false,
				// Status = GeneralStatus.Active // Nếu Enum yêu cầu
			};

			_context.News.Add(news);
			await _context.SaveChangesAsync();
			return news;
		}

		public async Task<bool> UpdateNewsAsync(int id, NewsDto dto)
		{
			var news = await _context.News.FirstOrDefaultAsync(n => n.NewsId == id && (n.IsDeleted == false || n.IsDeleted == null));
			if (news == null) return false;

			news.Title = dto.Title?.Trim();
			news.Description = dto.Description?.Trim();

			await _context.SaveChangesAsync();
			return true;
		}

		public async Task<bool> SoftDeleteNewsAsync(int id)
		{
			var news = await _context.News.FirstOrDefaultAsync(n => n.NewsId == id && (n.IsDeleted == false || n.IsDeleted == null));
			if (news == null) return false;

			news.IsDeleted = true;
			await _context.SaveChangesAsync();
			return true;
		}

		public async Task<bool> RestoreNewsAsync(int id)
		{
			var news = await _context.News.FirstOrDefaultAsync(n => n.NewsId == id && n.IsDeleted == true);
			if (news == null) return false;

			news.IsDeleted = false;
			await _context.SaveChangesAsync();
			return true;
		}

		public async Task<bool> HardDeleteNewsAsync(int id)
		{
			var news = await _context.News.FirstOrDefaultAsync(n => n.NewsId == id && n.IsDeleted == true);
			if (news == null) return false;

			_context.News.Remove(news);
			await _context.SaveChangesAsync();
			return true;
		}
	}
}