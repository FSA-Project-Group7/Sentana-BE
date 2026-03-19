using Sentana.API.DTOs.Contracts;
using Sentana.API.Enums;
using Sentana.API.Helpers;
using Sentana.API.Models;
using Sentana.API.Repositories;
using Sentana.API.Services.SStorage;
using Microsoft.EntityFrameworkCore; // Thêm thư viện này

namespace Sentana.API.Services
{
	public class ContractService : IContractService
	{
		private readonly IContractRepository _contractRepository;
		private readonly IMinioService _minioService;
		private readonly SentanaContext _context; // Đã thêm Context

		public ContractService(
			IContractRepository contractRepository,
			IMinioService minioService,
			SentanaContext context) // Đã inject Context
		{
			_contractRepository = contractRepository;
			_minioService = minioService;
			_context = context;
		}

		public async Task<ApiResponse<object>> CreateContractAsync(CreateContractDto request, int accountId)
		{
			if (request == null) return ApiResponse<object>.Fail(400, "Request không hợp lệ");
			if (request.StartDay >= request.EndDay) return ApiResponse<object>.Fail(400, "Ngày không hợp lệ");

			var manager = await _contractRepository.GetAccountAsync(accountId);
			if (manager == null) return ApiResponse<object>.Fail(404, "Account không tồn tại");
			if (manager.Role?.RoleName != "Manager") return ApiResponse<object>.Fail(403, "Chỉ Manager được tạo contract");

			var resident = await _contractRepository.GetAccountAsync(request.ResidentAccountId);
			if (resident == null || resident.Role?.RoleName != "Resident") return ApiResponse<object>.Fail(400, "Resident không hợp lệ");

			var hasContract = await _contractRepository.GetContractByAccountIdAsync(request.ResidentAccountId);
			if (hasContract != null) return ApiResponse<object>.Fail(400, "Resident đã có contract");

			var apartment = await _contractRepository.GetApartmentAsync(request.ApartmentId);
			if (apartment == null || apartment.Status != ApartmentStatus.Vacant) return ApiResponse<object>.Fail(400, "Apartment không hợp lệ hoặc không trống");

			// --- BẮT ĐẦU TRANSACTION ẢO BẰNG CÁCH CHỈ LƯU VÀO RAM ---

			// 1. Tạo entity Contract (Chưa Save vào DB vội)
			var contract = new Contract
			{
				ContractCode = "CT-" + DateTime.Now.Ticks,
				ApartmentId = request.ApartmentId,
				AccountId = request.ResidentAccountId,
				StartDay = request.StartDay,
				EndDay = request.EndDay,
				MonthlyRent = request.MonthlyRent,
				Deposit = request.Deposit,
				Status = GeneralStatus.Active,
				CreatedAt = DateTime.Now,
				CreatedBy = accountId
			};

			await _contractRepository.AddContractAsync(contract);
			// Vẫn phải Save LẦN 1 để lấy được cái contract.ContractId (Tự tăng) truyền cho MinIO
			await _contractRepository.SaveAsync();

			try
			{
				// 2. Upload file lên MinIO
				var fileUrl = await _minioService.UploadContractAsync(request.File, contract.ContractId, 1);
				contract.File = fileUrl; // Cập nhật link file vào hợp đồng

				// 3. Lưu phiên bản hợp đồng (Contract Version)
				var contractVersion = new ContractVersion
				{
					ContractId = contract.ContractId,
					VersionNumber = 1,
					File = fileUrl,
					CreatedAt = DateTime.Now,
					CreatedBy = accountId
				};
				await _context.ContractVersions.AddAsync(contractVersion);

				// 4. Gán Cư dân vào Phòng (GIẢI QUYẾT LỖI CƯ DÂN KHÔNG VÀO PHÒNG)
				var apartmentResident = new ApartmentResident
				{
					ApartmentId = request.ApartmentId,
					AccountId = request.ResidentAccountId,
					Status = GeneralStatus.Active,
					IsDeleted = false,
					CreatedAt = DateTime.Now,
					CreatedBy = accountId
				};
				await _context.ApartmentResidents.AddAsync(apartmentResident);

				// 5. Cập nhật trạng thái phòng thành Đang Thuê
				apartment.Status = ApartmentStatus.Occupied;

				// Lưu tất cả các thao tác còn lại (LẦN 2)
				await _context.SaveChangesAsync();

				return ApiResponse<object>.Success(contract, "Tạo hợp đồng và gán phòng thành công");
			}
			catch (Exception ex)
			{
				// LỖI: Cực kỳ quan trọng. Nếu MinIO hoặc đoạn code dưới lỗi, phải XÓA cái hợp đồng đã tạo ở Lần 1 đi.
				_context.Contracts.Remove(contract);
				await _context.SaveChangesAsync();

				return ApiResponse<object>.Fail(500, "Có lỗi xảy ra khi tạo hợp đồng: " + ex.Message);
			}
		}

		// ================= TERMINATE =================
		public async Task<ApiResponse<object>> TerminateContractAsync(int contractId, TerminateContractDto request)
		{
			var contract = await _contractRepository.GetContractWithApartmentAsync(contractId);

			if (contract == null) return ApiResponse<object>.Fail(404, "Không tìm thấy hợp đồng");
			if (contract.Status != GeneralStatus.Active) return ApiResponse<object>.Fail(400, "Contract không active");

			contract.Status = GeneralStatus.Inactive;
			contract.UpdatedAt = DateTime.Now;

			if (contract.Apartment != null)
				contract.Apartment.Status = ApartmentStatus.Vacant;

			var activeResidentInRoom = await _context.ApartmentResidents
				.FirstOrDefaultAsync(ar => ar.AccountId == contract.AccountId
										&& ar.ApartmentId == contract.ApartmentId
										&& ar.IsDeleted == false);

			if (activeResidentInRoom != null)
			{
				activeResidentInRoom.IsDeleted = true;
			}

			await _context.SaveChangesAsync(); // Lưu tất cả
			return ApiResponse<object>.Success(null, "Terminate và giải phóng phòng thành công");
		}

		// ================= EXTEND =================
		public async Task<ApiResponse<object>> ExtendContractAsync(int contractId, ExtendContractDto request)
		{
			var contract = await _contractRepository.GetContractWithApartmentAsync(contractId);

			if (contract == null) return ApiResponse<object>.Fail(404, "Không tìm thấy");
			if (contract.Status != GeneralStatus.Active) return ApiResponse<object>.Fail(400, "Contract không active");
			if (request.NewEndDate <= contract.EndDay) return ApiResponse<object>.Fail(400, "Ngày gia hạn phải lớn hơn ngày kết thúc cũ");

			contract.EndDay = request.NewEndDate;
			contract.UpdatedAt = DateTime.Now;

			await _contractRepository.SaveAsync();
			return ApiResponse<object>.Success(null, "Extend thành công");
		}

		// ================= UPDATE =================
		public async Task<ApiResponse<object>> UpdateContractAsync(int contractId, UpdateContractDto request)
		{
			var contract = await _contractRepository.GetContractDetailAsync(contractId);

			if (contract == null) return ApiResponse<object>.Fail(404, "Không tìm thấy");
			if (contract.Status != GeneralStatus.Active) return ApiResponse<object>.Fail(400, "Contract không active");

			if (request.StartDay.HasValue && request.EndDay.HasValue)
			{
				if (request.StartDay >= request.EndDay) return ApiResponse<object>.Fail(400, "Ngày không hợp lệ");
			}

			contract.StartDay = request.StartDay ?? contract.StartDay;
			contract.EndDay = request.EndDay ?? contract.EndDay;
			contract.MonthlyRent = request.MonthlyRent ?? contract.MonthlyRent;
			contract.Deposit = request.Deposit ?? contract.Deposit;
			contract.UpdatedAt = DateTime.Now;

			await _contractRepository.SaveAsync();
			return ApiResponse<object>.Success(null, "Update thành công");
		}

		public async Task<ApiResponse<object>> GetContractDetailAsync(int contractId)
		{
			var contract = await _contractRepository.GetContractDetailAsync(contractId);
			if (contract == null) return ApiResponse<object>.Fail(404, "Không tìm thấy");
			return ApiResponse<object>.Success(contract, "OK");
		}

		public async Task<ApiResponse<object>> GetContractListAsync()
		{
			var list = await _contractRepository.GetContractListAsync();
			return ApiResponse<object>.Success(list, "OK");
		}

		public async Task<ApiResponse<object>> GetMyContractAsync(int accountId)
		{
			var contract = await _contractRepository.GetContractByAccountIdAsync(accountId);
			if (contract == null) return ApiResponse<object>.Fail(404, "Không có contract");
			return ApiResponse<object>.Success(contract, "OK");
		}
	}
}