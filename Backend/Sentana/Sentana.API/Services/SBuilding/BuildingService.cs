using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Sentana.API.DTOs.Building;
using Sentana.API.DTOs.Resident;
using Sentana.API.Enums;
using Sentana.API.Models;

namespace Sentana.API.Services.SBuilding
{
    public class BuildingService : IBuildingService
    {
        private readonly SentanaContext _context;

        public BuildingService(SentanaContext context)
        {
            _context = context;
        }

		public async Task<BuildingResponseDto> CreateBuildingAsync(BuildingRequestDto dto, int managerId)
		{
			if (!dto.FloorNumber.HasValue || dto.FloorNumber <= 0)
				throw new ArgumentException("Số tầng là bắt buộc và phải lớn hơn 0.");
			if (string.IsNullOrWhiteSpace(dto.BuildingCode) || string.IsNullOrWhiteSpace(dto.BuildingName))
				throw new ArgumentException("Mã và Tên tòa nhà không được để trống.");

			var isExist = await _context.Buildings.AnyAsync(b => b.BuildingCode != null && b.BuildingCode.ToLower() == dto.BuildingCode.ToLower());

			if (isExist) throw new InvalidOperationException($"Mã tòa nhà '{dto.BuildingCode}' đã tồn tại (hoặc đang nằm trong thùng rác). Vui lòng chọn mã khác!");

			int calculatedApartmentNumber = dto.FloorNumber.Value * 10;

			var newBuilding = new Building
			{
				BuildingName = dto.BuildingName,
				BuildingCode = dto.BuildingCode.ToUpper(),
				Address = dto.Address,
				City = dto.City,
				FloorNumber = dto.FloorNumber,
				ApartmentNumber = calculatedApartmentNumber,
				Status = (GeneralStatus)1,
				CreatedAt = DateTime.UtcNow,
				IsDeleted = false,
				CreatedBy = managerId,
				ManagerId = managerId
            };

			_context.Buildings.Add(newBuilding);
			await _context.SaveChangesAsync();

			var newApartments = new List<Apartment>();

			for (int floor = 1; floor <= newBuilding.FloorNumber; floor++)
			{
				for (int aptIndex = 1; aptIndex <= 10; aptIndex++)
				{
					int roomNumber = (floor * 100) + aptIndex;

					newApartments.Add(new Apartment
					{
						BuildingId = newBuilding.BuildingId,
						ApartmentCode = $"{newBuilding.BuildingCode}-{roomNumber}",
						ApartmentName = $"Phòng {roomNumber} - {newBuilding.BuildingName}",
						ApartmentNumber = roomNumber,
						FloorNumber = floor,
						Area = 0,
						Status = ApartmentStatus.Vacant,
						CreatedAt = DateTime.UtcNow,
						CreatedBy = managerId,
						IsDeleted = false
					});
				}
			}

			await _context.Apartments.AddRangeAsync(newApartments);
			await _context.SaveChangesAsync();

			return MapToResponseDto(newBuilding);
		}

		public async Task<BuildingResponseDto> UpdateBuildingAsync(int id, BuildingRequestDto dto, int managerId)
		{
			var existingBuilding = await _context.Buildings
				.Include(b => b.Apartments)
					.ThenInclude(a => a.Contracts)
				.FirstOrDefaultAsync(b => b.BuildingId == id && b.IsDeleted == false);

			if (existingBuilding == null) throw new InvalidOperationException("Không tìm thấy tòa nhà.");

			if (!string.IsNullOrWhiteSpace(dto.BuildingName) && dto.BuildingName != existingBuilding.BuildingName)
				existingBuilding.BuildingName = dto.BuildingName;

			if (!string.IsNullOrWhiteSpace(dto.BuildingCode) && dto.BuildingCode != existingBuilding.BuildingCode)
			{
				var isExist = await _context.Buildings.AnyAsync(b => b.BuildingCode != null && b.BuildingCode.ToLower() == dto.BuildingCode.ToLower());
				if (isExist) throw new InvalidOperationException($"Mã tòa nhà '{dto.BuildingCode}' đã tồn tại. Vui lòng chọn mã khác!");

				existingBuilding.BuildingCode = dto.BuildingCode;
			}

			if (dto.Address != null) existingBuilding.Address = dto.Address;
			if (dto.City != null) existingBuilding.City = dto.City;

            if (dto.FloorNumber.HasValue && dto.FloorNumber.Value != existingBuilding.FloorNumber)
            {
                int oldFloorNumber = existingBuilding.FloorNumber ?? 0;
                int newFloorNumber = dto.FloorNumber.Value;

                if (newFloorNumber < oldFloorNumber)
                    throw new ArgumentException("Không thể giảm số tầng của tòa nhà đã có dữ liệu.");
                existingBuilding.FloorNumber = newFloorNumber;
                existingBuilding.ApartmentNumber = newFloorNumber * 10;
                var additionalApartments = new List<Apartment>();
                for (int floor = oldFloorNumber + 1; floor <= newFloorNumber; floor++)
                {
                    for (int aptIndex = 1; aptIndex <= 10; aptIndex++)
                    {
                        int roomNumber = (floor * 100) + aptIndex;
                        additionalApartments.Add(new Apartment
                        {
                            BuildingId = existingBuilding.BuildingId,
                            ApartmentCode = $"{existingBuilding.BuildingCode}-{roomNumber}",
                            ApartmentName = $"Phòng {roomNumber} - {existingBuilding.BuildingName}",
                            ApartmentNumber = roomNumber,
                            FloorNumber = floor,
                            Status = ApartmentStatus.Vacant,
                            CreatedAt = DateTime.UtcNow,
                            CreatedBy = managerId,
                            IsDeleted = false
                        });
                    }
                }
                if (additionalApartments.Any())
                {
                    await _context.Apartments.AddRangeAsync(additionalApartments);
                }
            }

            if (dto.Status.HasValue && existingBuilding.Status != (GeneralStatus)dto.Status.Value)
			{
				var newStatus = (GeneralStatus)dto.Status.Value;
				existingBuilding.Status = newStatus;

				if (newStatus == GeneralStatus.Inactive)
				{
					foreach (var apt in existingBuilding.Apartments.Where(a => a.IsDeleted == false))
					{
						apt.Status = ApartmentStatus.Maintenance;
					}
				}
				else if (newStatus == GeneralStatus.Active)
				{
					var today = DateOnly.FromDateTime(DateTime.Now);
					foreach (var apt in existingBuilding.Apartments.Where(a => a.IsDeleted == false))
					{
						bool hasActiveContract = apt.Contracts.Any(c => c.Status == GeneralStatus.Active && c.EndDay >= today);
						apt.Status = hasActiveContract ? ApartmentStatus.Occupied : ApartmentStatus.Vacant;
					}
				}
			}

			existingBuilding.UpdatedAt = DateTime.UtcNow;
			existingBuilding.UpdatedBy = managerId;

            _context.Buildings.Update(existingBuilding);
			await _context.SaveChangesAsync();

			return MapToResponseDto(existingBuilding);
		}

		public async Task<bool> DeleteBuildingAsync(int id, ClaimsPrincipal user)
		{
			var existingBuilding = await _context.Buildings
				.Include(b => b.Apartments)
					.ThenInclude(a => a.Contracts)
				.Include(b => b.Apartments)
					.ThenInclude(a => a.ApartmentResidents)
				.FirstOrDefaultAsync(b => b.BuildingId == id && b.IsDeleted == false);

			if (existingBuilding == null) throw new InvalidOperationException("Không tìm thấy tòa nhà.");

			var activeApartments = existingBuilding.Apartments.Where(a => a.IsDeleted == false).ToList();

			bool hasOccupiedOrContractedApartments = activeApartments.Any(a =>
				a.Status != ApartmentStatus.Vacant ||
				a.Contracts.Any(c => c.IsDeleted == false) ||
				a.ApartmentResidents.Any(ar => ar.IsDeleted == false)
			);

			if (hasOccupiedOrContractedApartments)
				throw new InvalidOperationException("Không thể đưa vào thùng rác! Tòa nhà này đang có căn hộ có người ở, có hợp đồng hoặc có dữ liệu cư dân.");

			int? accountId = null;
			var accountIdClaim = user?.FindFirst("AccountId");
			if (accountIdClaim != null && int.TryParse(accountIdClaim.Value, out var parsedAccountId))
			{
				accountId = parsedAccountId;
			}

			existingBuilding.IsDeleted = true;
			existingBuilding.UpdatedAt = DateTime.UtcNow;
			existingBuilding.UpdatedBy = accountId;

			foreach (var apt in activeApartments)
			{
				apt.IsDeleted = true;
				apt.UpdatedAt = DateTime.UtcNow;
				apt.UpdatedBy = accountId;
			}

			_context.Buildings.Update(existingBuilding);
			await _context.SaveChangesAsync();
			return true;
		}

		//Lấy danh sách đã xóa mềm
		public async Task<IEnumerable<BuildingResponseDto>> GetDeletedBuildingsAsync(int? managerId = null)
		{
			return await _context.Buildings
				.Where(b => b.ManagerId == managerId && b.IsDeleted == true) 
				.Select(b => new BuildingResponseDto
				{
					BuildingId = b.BuildingId,
					BuildingName = b.BuildingName,
					BuildingCode = b.BuildingCode,
					Address = b.Address,
					City = b.City,
					FloorNumber = b.FloorNumber,
					ApartmentNumber = b.ApartmentNumber,
					Status = (byte?)b.Status
				})
				.ToListAsync();
		}

		//Khôi phục tòa nhà đã xóa mềm
		public async Task<bool> RestoreBuildingAsync(int id)
		{
			var building = await _context.Buildings
				.Include(b => b.Apartments)
				.FirstOrDefaultAsync(b => b.BuildingId == id && b.IsDeleted == true);

			if (building == null) throw new InvalidOperationException("Không tìm thấy tòa nhà trong thùng rác.");

			building.IsDeleted = false; 
			building.UpdatedAt = DateTime.UtcNow;

			foreach (var apt in building.Apartments.Where(a => a.IsDeleted == true))
			{
				apt.IsDeleted = false;
				apt.UpdatedAt = DateTime.UtcNow;
			}

			_context.Buildings.Update(building);
			await _context.SaveChangesAsync();
			return true;
		}

		//Xóa vĩnh viễn
		public async Task<bool> HardDeleteBuildingAsync(int id)
		{
			var building = await _context.Buildings
				.Include(b => b.Apartments)
				.FirstOrDefaultAsync(b => b.BuildingId == id && b.IsDeleted == true);

			if (building == null) throw new InvalidOperationException("Không tìm thấy tòa nhà trong thùng rác.");

			if (building.Apartments.Any())
			{
				_context.Apartments.RemoveRange(building.Apartments);
			}

			_context.Buildings.Remove(building);
			await _context.SaveChangesAsync();
			return true;
		}
		private static BuildingResponseDto MapToResponseDto(Building building)
        {
            return new BuildingResponseDto
            {
                BuildingId = building.BuildingId,
                BuildingName = building.BuildingName,
                BuildingCode = building.BuildingCode,
                Address = building.Address,
                City = building.City,
                FloorNumber = building.FloorNumber,
                ApartmentNumber = building.ApartmentNumber,
				Status = (byte?)building.Status
			};
        } 

		public async Task<IEnumerable<BuildingResponseDto>> GetBuildingListAsync(int? managerId = null)
		{
			return await _context.Buildings
			.Where(b => b.ManagerId == managerId && b.IsDeleted == false)
			.Select(b => new BuildingResponseDto
			{
				BuildingId = b.BuildingId,
				BuildingName = b.BuildingName,
				BuildingCode = b.BuildingCode,
				Address = b.Address,
				City = b.City,
				FloorNumber = b.FloorNumber,
				ApartmentNumber = b.ApartmentNumber,
				Status = (byte?)b.Status
			})
			.ToListAsync();
		}

        // US79 - View Occupancy Dashboard (Manager)
        public async Task<OccupancyDashboardDto> GetOccupancyDashboardAsync(int managerId)
        {
            var buildings = await _context.Buildings
                .Include(b => b.Apartments)
                .Where(b => b.ManagerId == managerId && b.IsDeleted == false)
                .ToListAsync();

            var result = new OccupancyDashboardDto();

            foreach (var building in buildings)
            {
                var activeApts = building.Apartments.Where(a => a.IsDeleted == false).ToList();
                int total = activeApts.Count;
                int occupied = activeApts.Count(a => a.Status == ApartmentStatus.Occupied);
                int vacant = activeApts.Count(a => a.Status == ApartmentStatus.Vacant);
                int maintenance = activeApts.Count(a => a.Status == ApartmentStatus.Maintenance);

                result.TotalApartments += total;
                result.OccupiedApartments += occupied;
                result.VacantApartments += vacant;
                result.MaintenanceApartments += maintenance;

                result.ByBuilding.Add(new BuildingOccupancyDto
                {
                    BuildingId = building.BuildingId,
                    BuildingName = building.BuildingName,
                    BuildingCode = building.BuildingCode,
                    TotalApartments = total,
                    OccupiedApartments = occupied,
                    VacantApartments = vacant,
                    MaintenanceApartments = maintenance,
                    OccupancyRate = total > 0 ? Math.Round((double)occupied / total * 100, 2) : 0
                });
            }

            result.OccupancyRate = result.TotalApartments > 0
                ? Math.Round((double)result.OccupiedApartments / result.TotalApartments * 100, 2)
                : 0;

            return result;
        }

        // US80 - View Total Residents KPI (Manager)
        public async Task<ResidentKpiDto> GetResidentKpiAsync(int managerId)
        {
            // Lấy tất cả buildings thuộc manager
            var buildingIds = await _context.Buildings
                .Where(b => b.ManagerId == managerId && b.IsDeleted == false)
                .Select(b => b.BuildingId)
                .ToListAsync();

            // Lấy tất cả apartmentIds thuộc những buildings đó
            var apartmentIds = await _context.Apartments
                .Where(a => buildingIds.Contains(a.BuildingId ?? 0) && a.IsDeleted == false)
                .Select(a => a.ApartmentId)
                .ToListAsync();

            // Lấy tất cả cư dân (RoleId = 2) đã được assign vào các apartment của manager
            var residents = await _context.ApartmentResidents
                .Include(ar => ar.Account)
                .Where(ar =>
                    apartmentIds.Contains(ar.ApartmentId ?? 0) &&
                    ar.IsDeleted == false &&
                    ar.Account != null &&
                    ar.Account.RoleId == 2 &&   // Role = Resident
                    ar.Account.IsDeleted == false)
                .Select(ar => ar.Account!)
                .Distinct()
                .ToListAsync();

            // Cư dân mới trong tháng hiện tại
            var now = DateTime.Now;
            int newThisMonth = residents.Count(r =>
                r.CreatedAt.HasValue &&
                r.CreatedAt.Value.Month == now.Month &&
                r.CreatedAt.Value.Year == now.Year);

            // Đếm cư dân theo tòa nhà
            var byBuilding = new List<BuildingResidentCountDto>();
            var buildings = await _context.Buildings
                .Where(b => b.ManagerId == managerId && b.IsDeleted == false)
                .ToListAsync();

            foreach (var building in buildings)
            {
                var buildingAptIds = await _context.Apartments
                    .Where(a => a.BuildingId == building.BuildingId && a.IsDeleted == false)
                    .Select(a => a.ApartmentId)
                    .ToListAsync();

                var count = await _context.ApartmentResidents
                    .Where(ar =>
                        buildingAptIds.Contains(ar.ApartmentId ?? 0) &&
                        ar.IsDeleted == false &&
                        ar.Status == GeneralStatus.Active)
                    .Select(ar => ar.AccountId)
                    .Distinct()
                    .CountAsync();

                byBuilding.Add(new BuildingResidentCountDto
                {
                    BuildingId = building.BuildingId,
                    BuildingName = building.BuildingName,
                    BuildingCode = building.BuildingCode,
                    ResidentCount = count
                });
            }

            // Tổng cư dân (tài khoản có role Resident thuộc quản lý của manager)
            var totalResidentAccounts = await _context.Accounts
                .Where(a => a.RoleId == 2 && a.IsDeleted == false &&
                    _context.ApartmentResidents
                        .Where(ar => apartmentIds.Contains(ar.ApartmentId ?? 0) && ar.IsDeleted == false)
                        .Select(ar => ar.AccountId)
                        .Distinct()
                        .Contains(a.AccountId))
                .ToListAsync();

            return new ResidentKpiDto
            {
                TotalResidents = totalResidentAccounts.Count,
                ActiveResidents = totalResidentAccounts.Count(r => r.Status == GeneralStatus.Active),
                InactiveResidents = totalResidentAccounts.Count(r => r.Status == GeneralStatus.Inactive),
                ResidentsInRoom = residents.Count,
                ResidentsNotInRoom = totalResidentAccounts.Count - residents.Count,
                NewResidentsThisMonth = newThisMonth,
                ByBuilding = byBuilding
            };
        }
    }
}
