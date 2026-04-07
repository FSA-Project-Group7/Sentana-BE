using Microsoft.EntityFrameworkCore;
using Sentana.API.DTOs.Contracts;
using Sentana.API.Enums;
using Sentana.API.Helpers;
using Sentana.API.Models;
using Sentana.API.Repositories;
using Sentana.API.Services.SStorage;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Sentana.API.Services
{
    public class ContractService : IContractService
    {
        private readonly IContractRepository _contractRepository;
        private readonly IMinioService _minioService;
        private readonly SentanaContext _context;

        public ContractService(
            IContractRepository contractRepository,
            IMinioService minioService,
            SentanaContext context)
        {
            _contractRepository = contractRepository;
            _minioService = minioService;
            _context = context;
        }

        public async Task<ApiResponse<object>> CreateContractAsync(CreateContractDto request, int accountId)
        {
            if (request == null) return ApiResponse<object>.Fail(400, "Request không hợp lệ");
            if (request.StartDay >= request.EndDay) return ApiResponse<object>.Fail(400, "Ngày bắt đầu phải trước ngày kết thúc");
            if (request.MonthlyRent < 0 || request.Deposit < 0) return ApiResponse<object>.Fail(400, "Tiền thuê và tiền cọc không được là số âm");
            if (request.File == null || !request.File.ContentType.Contains("pdf")) return ApiResponse<object>.Fail(400, "File PDF là bắt buộc");
            if (request.File.Length > 5 * 1024 * 1024) return ApiResponse<object>.Fail(400, "File quá lớn (max 5MB)");

            var manager = await _contractRepository.GetAccountAsync(accountId);
            if (manager == null || manager.Role?.RoleName != "Manager")
                return ApiResponse<object>.Fail(403, "Chỉ Manager được quyền tạo hợp đồng");

            var residentAccount = await _context.Accounts.Include(a => a.Role)
                .FirstOrDefaultAsync(a => a.AccountId == request.ResidentAccountId && a.IsDeleted == false);

            if (residentAccount == null || residentAccount.Status != GeneralStatus.Active || residentAccount.Role?.RoleName != "Resident")
                return ApiResponse<object>.Fail(400, "Tài khoản chủ hợp đồng không hợp lệ hoặc không phải Cư dân");

            var apartment = await _contractRepository.GetApartmentAsync(request.ApartmentId);
            if (apartment == null) return ApiResponse<object>.Fail(400, "Căn hộ không tồn tại");
            if (apartment.Status == ApartmentStatus.Maintenance) return ApiResponse<object>.Fail(400, "Căn hộ đang bảo trì");

            // Check chống trùng lịch an toàn tuyệt đối
            var overlap = await _context.Contracts.AnyAsync(c =>
                c.ApartmentId == request.ApartmentId && c.Status == GeneralStatus.Active && c.IsDeleted == false &&
                (request.StartDay < c.EndDay && request.EndDay > c.StartDay));

            if (overlap) return ApiResponse<object>.Fail(400, "Thời gian hợp đồng bị trùng lặp với hợp đồng khác đang Active.");

            // 👉 FIX 1: ĐÁNH CHẶN NGƯỜI Ở GHÉP - Ngăn lỗi 500 do FE gửi ID ảo
            if (request.AdditionalResidents != null && request.AdditionalResidents.Any())
            {
                foreach (var r in request.AdditionalResidents)
                {
                    if (r.AccountId <= 0)
                    {
                        return ApiResponse<object>.Fail(400, $"Frontend đang gửi dữ liệu người ở ghép bị lỗi (AccountId = {r.AccountId}). Vui lòng check lại Payload!");
                    }

                    var exists = await _context.Accounts.AnyAsync(a => a.AccountId == r.AccountId && a.IsDeleted == false); if (!exists)
                    {
                        return ApiResponse<object>.Fail(400, $"Tài khoản người ở ghép mang ID {r.AccountId} không tồn tại trong DB!");
                    }
                }
            }

            Dictionary<int, decimal?> systemServices = new Dictionary<int, decimal?>();
            if (request.Services != null && request.Services.Any())
            {
                var serviceIds = request.Services.Select(s => s.ServiceId).ToList();
                systemServices = await _context.Services
                    .Where(s => serviceIds.Contains(s.ServiceId) && s.Status == GeneralStatus.Active)
                    .ToDictionaryAsync(s => s.ServiceId, s => s.ServiceFee);
            }

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var fileUrl = await _minioService.UploadContractAsync(request.File, 0, 1);

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
                    CreatedBy = accountId,
                    File = fileUrl,
                    IsDeleted = false
                };
                await _context.Contracts.AddAsync(contract);
                await _context.SaveChangesAsync();

                var version = new ContractVersion
                {
                    ContractId = contract.ContractId,
                    VersionNumber = 1,
                    File = fileUrl,
                    CreatedAt = DateTime.Now,
                    CreatedBy = accountId
                };
                await _context.ContractVersions.AddAsync(version);
                await _context.SaveChangesAsync();

                contract.CurrentVersionId = version.VersionId;

                await _context.ApartmentResidents.AddAsync(new ApartmentResident
                {
                    ApartmentId = request.ApartmentId,
                    AccountId = request.ResidentAccountId,
                    RelationshipId = 1,
                    Status = GeneralStatus.Active,
                    CreatedAt = DateTime.Now,
                    CreatedBy = accountId,
                    IsDeleted = false
                });

                if (request.AdditionalResidents != null && request.AdditionalResidents.Any())
                {
                    foreach (var r in request.AdditionalResidents)
                    {
                        await _context.ApartmentResidents.AddAsync(new ApartmentResident
                        {
                            ApartmentId = request.ApartmentId,
                            AccountId = r.AccountId,
                            RelationshipId = r.RelationshipId,
                            Status = GeneralStatus.Active,
                            CreatedAt = DateTime.Now,
                            CreatedBy = accountId,
                            IsDeleted = false
                        });
                    }
                }

                if (request.Services != null && request.Services.Any())
                {
                    foreach (var s in request.Services)
                    {
                        if (systemServices.ContainsKey(s.ServiceId))
                        {
                            await _context.ApartmentServices.AddAsync(new ApartmentService
                            {
                                ApartmentId = request.ApartmentId,
                                ServiceId = s.ServiceId,
                                ActualPrice = s.ActualPrice ?? systemServices[s.ServiceId],
                                StartDay = request.StartDay,
                                Status = GeneralStatus.Active,
                                CreatedAt = DateTime.Now,
                                CreatedBy = accountId,
                                IsDeleted = false
                            });
                        }
                    }
                }

                apartment.Status = ApartmentStatus.Occupied;

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return ApiResponse<object>.Success(contract, "Tạo hợp đồng thành công! Đã gán thành viên và đăng ký dịch vụ.");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return ApiResponse<object>.Fail(500, "Lỗi server: " + ex.Message);
            }
        }

        public async Task<ApiResponse<object>> UpdateContractAsync(int contractId, UpdateContractDto request)
        {
            var contract = await _contractRepository.GetContractDetailAsync(contractId);

            if (contract == null) return ApiResponse<object>.Fail(404, "Không tìm thấy");
            if (contract.Status != GeneralStatus.Active) return ApiResponse<object>.Fail(400, "Chỉ update contract active");
            if (request.MonthlyRent < 0 || request.Deposit < 0) return ApiResponse<object>.Fail(400, "Tiền thuê và tiền cọc không được là số âm");

            var newStart = request.StartDay ?? contract.StartDay;
            var newEnd = request.EndDay ?? contract.EndDay;

            if (newStart >= newEnd) return ApiResponse<object>.Fail(400, "Ngày không hợp lệ");

            var overlap = await _context.Contracts.AnyAsync(c =>
                c.ContractId != contractId &&
                c.ApartmentId == contract.ApartmentId &&
                c.IsDeleted == false &&
                c.Status == GeneralStatus.Active &&
                (newStart < c.EndDay && newEnd > c.StartDay));

            if (overlap) return ApiResponse<object>.Fail(400, "Thời gian hợp đồng bị trùng lặp");

            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                contract.StartDay = newStart;
                contract.EndDay = newEnd;
                contract.MonthlyRent = request.MonthlyRent ?? contract.MonthlyRent;
                contract.Deposit = request.Deposit ?? contract.Deposit;
                contract.UpdatedAt = DateTime.Now;

                // 1. Xử lý File PDF
                if (request.File != null)
                {
                    if (!request.File.ContentType.Contains("pdf"))
                        return ApiResponse<object>.Fail(400, "Chỉ PDF");
                    if (request.File.Length > 5 * 1024 * 1024)
                        return ApiResponse<object>.Fail(400, "File quá lớn (max 5MB)");

                    var fileUrl = await _minioService.UploadContractAsync(request.File, contractId, 2);
                    var lastVersion = await _contractRepository.GetLatestContractVersionAsync(contractId);
                    var newVersion = (lastVersion?.VersionNumber ?? 0) + 1;

                    var version = new ContractVersion
                    {
                        ContractId = contractId,
                        VersionNumber = newVersion,
                        File = fileUrl,
                        CreatedAt = DateTime.Now
                    };

                    await _context.ContractVersions.AddAsync(version);
                    await _context.SaveChangesAsync();

                    contract.File = fileUrl;
                    contract.CurrentVersionId = version.VersionId;
                }

                // 2. Xử lý Cư dân phụ (AdditionalResidents)
                if (contract.ApartmentId.HasValue && request.AdditionalResidents != null)
                {
                    int aptId = contract.ApartmentId.Value;

                    var existingResidents = await _context.ApartmentResidents
                        .Where(r => r.ApartmentId == aptId && r.AccountId != contract.AccountId)
                        .ToListAsync();

                    foreach (var r in existingResidents)
                    {
                        r.IsDeleted = true; // Xóa mềm toàn bộ trước
                    }

                    foreach (var newRes in request.AdditionalResidents)
                    {
                        var exist = existingResidents.FirstOrDefault(x => x.AccountId == newRes.AccountId);
                        if (exist != null)
                        {
                            exist.IsDeleted = false; // Mở lại nếu có gửi lên
                            exist.RelationshipId = newRes.RelationshipId;
                        }
                        else
                        {
                            await _context.ApartmentResidents.AddAsync(new ApartmentResident
                            {
                                ApartmentId = aptId,
                                AccountId = newRes.AccountId,
                                RelationshipId = newRes.RelationshipId,
                                Status = GeneralStatus.Active,
                                CreatedAt = DateTime.Now,
                                IsDeleted = false
                            });
                        }
                    }
                }

                // 3. Xử lý Dịch vụ cố định (Services)
                if (contract.ApartmentId.HasValue && request.Services != null)
                {
                    int aptId = contract.ApartmentId.Value;

                    var existingServices = await _context.ApartmentServices
                        .Where(s => s.ApartmentId == aptId)
                        .ToListAsync();

                    foreach (var s in existingServices)
                    {
                        s.IsDeleted = true; // Xóa mềm toàn bộ trước
                    }

                    var serviceIds = request.Services.Select(s => s.ServiceId).ToList();
                    var systemServices = await _context.Services
                        .Where(s => serviceIds.Contains(s.ServiceId))
                        .ToDictionaryAsync(s => s.ServiceId, s => s.ServiceFee);

                    foreach (var newSrv in request.Services)
                    {
                        if (systemServices.ContainsKey(newSrv.ServiceId))
                        {
                            var exist = existingServices.FirstOrDefault(x => x.ServiceId == newSrv.ServiceId);
                            if (exist != null)
                            {
                                exist.IsDeleted = false; // Mở lại nếu có gửi lên
                                exist.ActualPrice = newSrv.ActualPrice ?? systemServices[newSrv.ServiceId];
                            }
                            else
                            {
                                await _context.ApartmentServices.AddAsync(new ApartmentService
                                {
                                    ApartmentId = aptId,
                                    ServiceId = newSrv.ServiceId,
                                    ActualPrice = newSrv.ActualPrice ?? systemServices[newSrv.ServiceId],
                                    StartDay = contract.StartDay,
                                    Status = GeneralStatus.Active,
                                    CreatedAt = DateTime.Now,
                                    IsDeleted = false
                                });
                            }
                        }
                    }
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return ApiResponse<object>.Success(null, "Update thành công");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return ApiResponse<object>.Fail(500, ex.Message);
            }
        }

        public async Task<ApiResponse<object>> TerminateContractAsync(int contractId, TerminateContractDto request)
        {
            var contract = await _contractRepository.GetContractWithApartmentAsync(contractId);

            if (contract == null) return ApiResponse<object>.Fail(404, "Không tìm thấy hợp đồng");
            if (contract.Status != GeneralStatus.Active) return ApiResponse<object>.Fail(400, "Contract không active");

            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // 1. Lấy dữ liệu tài chính (Hóa đơn & Thanh toán)
                var invoices = await _context.Invoices
                    .Where(x => x.ContractId == contractId)
                    .ToListAsync();

                var invoiceIds = invoices.Select(x => x.InvoiceId).ToList();

                var payments = await _context.PaymentTransactions
                    .Where(x => x.InvoiceId != null && invoiceIds.Contains(x.InvoiceId.Value))
                    .ToListAsync();

                // 2. Tính toán các con số
                decimal totalInvoice = invoices.Sum(x => x.TotalMoney ?? 0);
                decimal totalPaid = payments.Sum(x => x.AmountPaid ?? 0);
                decimal additionalCost = request.AdditionalCost;

                // Công thức chuẩn: Refund = Đã trả - Nợ cũ - Phụ phí phát sinh
                decimal refund = totalPaid - totalInvoice - additionalCost;
                bool isFullyPaid = totalPaid >= (totalInvoice + additionalCost);

                // ========================================================
                // GIẢI QUYẾT VẤN ĐỀ 2: LƯU DỮ LIỆU TÀI CHÍNH VÀO DB
                // ========================================================
                contract.Status = GeneralStatus.Inactive;
                contract.EndDay = request.TerminationDate;
                contract.UpdatedAt = DateTime.Now;

                // LƯU CÁC TRƯỜNG QUAN TRỌNG
                contract.AdditionalCost = additionalCost;
                contract.RefundAmount = refund;
                contract.TerminationReason = request.TerminationReason;

                // ========================================================
                // GIẢI QUYẾT VẤN ĐỀ 1: PHÂN LUỒNG TRẠNG THÁI QUYẾT TOÁN
                // ========================================================
                if (additionalCost > 0)
                {
                    // Nếu có phạt/bồi thường -> Chờ khách nộp tiền
                    contract.SettlementStatus = SettlementStatus.PendingSettlement;
                }
                else
                {
                    // Nếu không có phạt -> Sạch sẽ, hoàn tất luôn
                    contract.SettlementStatus = SettlementStatus.Settled;
                    contract.SettledAt = DateTime.Now;
                }

                // 3. Xử lý trả phòng
                if (contract.Apartment != null && contract.Apartment.Status == ApartmentStatus.Occupied)
                {
                    contract.Apartment.Status = ApartmentStatus.Vacant;
                }

                // 4. Xử lý Cư dân và Dịch vụ (Theo cấu trúc cũ của bạn)
                if (contract.ApartmentId.HasValue)
                {
                    int apartmentId = contract.ApartmentId.Value;

                    var residents = await _context.ApartmentResidents
                        .Where(x => x.ApartmentId == apartmentId && x.IsDeleted == false)
                        .ToListAsync();

                    foreach (var r in residents)
                    {
                        r.IsDeleted = true;
                        r.Status = GeneralStatus.Inactive;
                    }

                    var services = await _context.ApartmentServices
                        .Where(x => x.ApartmentId == apartmentId && x.IsDeleted == false)
                        .ToListAsync();

                    foreach (var s in services)
                    {
                        s.EndDay = request.TerminationDate;
                        s.IsDeleted = true;
                        s.Status = GeneralStatus.Inactive;
                    }
                }

                // CHỐT LƯU VÀO DB
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                string paymentStatus = isFullyPaid ? "Đã thanh toán đủ" : "Chưa thanh toán đủ";

                return ApiResponse<object>.Success(new
                {
                    TotalInvoice = totalInvoice,
                    TotalPaid = totalPaid,
                    AdditionalCost = additionalCost,
                    RefundAmount = refund,
                    IsFullyPaid = isFullyPaid,
                    PaymentStatus = paymentStatus,
                    SettlementStatus = contract.SettlementStatus.ToString() // Trả về cho FE biết luôn
                }, "Chấm dứt hợp đồng thành công và đã lưu trữ sao kê tài chính.");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return ApiResponse<object>.Fail(500, ex.Message);
            }
        }

        public async Task<ApiResponse<object>> ExtendContractAsync(int contractId, ExtendContractDto request)
        {
            var contract = await _contractRepository.GetContractWithApartmentAsync(contractId);

            if (contract == null) return ApiResponse<object>.Fail(404, "Không tìm thấy");
            if (contract.Status != GeneralStatus.Active) return ApiResponse<object>.Fail(400, "Contract không active");
            if (request.NewEndDate <= contract.EndDay) return ApiResponse<object>.Fail(400, "Ngày không hợp lệ");

            var overlap = await _context.Contracts.AnyAsync(c =>
                c.ContractId != contractId &&
                c.ApartmentId == contract.ApartmentId &&
                c.IsDeleted == false &&
                c.Status == GeneralStatus.Active &&
                (contract.StartDay < c.EndDay && request.NewEndDate > c.StartDay)
            );

            if (overlap) return ApiResponse<object>.Fail(400, "Thời gian gia hạn bị trùng lặp với hợp đồng khác.");

            contract.EndDay = request.NewEndDate;
            contract.UpdatedAt = DateTime.Now;

            await _context.SaveChangesAsync();

            return ApiResponse<object>.Success(null, "Extend thành công");
        }

        // 👉 FIX 2: TÍNH TOÁN REFUND TRẢ VỀ CHO MANAGER (BUG 58)
        public async Task<ApiResponse<object>> GetContractDetailAsync(int contractId)
        {
            var contract = await _context.Contracts
                .Include(c => c.Account).ThenInclude(a => a.Info)
                .Include(c => c.Apartment).ThenInclude(a => a.ApartmentResidents)
                .Include(c => c.Apartment).ThenInclude(a => a.ApartmentServices)
                .Include(c => c.CurrentVersion)
                .FirstOrDefaultAsync(c => c.ContractId == contractId && c.IsDeleted == false);

            if (contract == null) return ApiResponse<object>.Fail(404, "Không tìm thấy");

            decimal? refundAmount = null;
            if (contract.Status == GeneralStatus.Inactive) // 0 = Terminated
            {
                var invoices = await _context.Invoices.Where(x => x.ContractId == contractId).ToListAsync();
                var invoiceIds = invoices.Select(x => x.InvoiceId).ToList();
                var payments = await _context.PaymentTransactions.Where(x => x.InvoiceId != null && invoiceIds.Contains(x.InvoiceId.Value)).ToListAsync();

                decimal totalInvoice = invoices.Sum(x => x.TotalMoney ?? 0);
                decimal totalPaid = payments.Sum(x => x.AmountPaid ?? 0);
                decimal additionalCost = contract.AdditionalCost ?? 0;

                refundAmount = totalPaid - totalInvoice - additionalCost;
            }

            var detailDto = new ContractDetailDto
            {
                ContractId = contract.ContractId,
                ContractCode = contract.ContractCode,
                ApartmentId = contract.ApartmentId,
                ApartmentName = contract.Apartment?.ApartmentName,
                AccountId = contract.AccountId,
                TenantName = contract.Account?.Info?.FullName,

                StartDay = contract.StartDay,
                EndDay = contract.EndDay,

                MonthlyRent = contract.MonthlyRent,
                Deposit = contract.Deposit,
                Status = contract.Status,
                CreatedAt = contract.CreatedAt,
                File = contract.CurrentVersion?.File,

                // TRẢ VỀ CÁC THÔNG SỐ TÀI CHÍNH
                AdditionalCost = contract.AdditionalCost,
                TerminationReason = contract.TerminationReason,
                RefundAmount = refundAmount
            };

            // Lấy danh sách người ở cùng
            if (contract.Apartment != null && contract.Apartment.ApartmentResidents != null)
            {
                detailDto.AdditionalResidents = contract.Apartment.ApartmentResidents
                    .Where(r => r.IsDeleted == false && r.AccountId != contract.AccountId) // Bỏ qua chủ hộ
                    .Select(r => new ResidentItemDto
                    {
                        AccountId = r.AccountId ?? 0,
                        RelationshipId = r.RelationshipId ?? 0
                    })
                    .ToList();
            }

            // Lấy danh sách dịch vụ
            if (contract.Apartment != null && contract.Apartment.ApartmentServices != null)
            {
                detailDto.SelectedServices = contract.Apartment.ApartmentServices
                    .Where(s => s.IsDeleted == false)
                    .Select(s => new ServiceItemDto
                    {
                        ServiceId = s.ServiceId ?? 0,
                        ActualPrice = s.ActualPrice
                    })
                    .ToList();
            }

            return ApiResponse<object>.Success(detailDto, "OK");
        }

        public async Task<ApiResponse<object>> GetContractListAsync()
        {
            var list = await _contractRepository.GetContractListAsync();
            return ApiResponse<object>.Success(list, "OK");
        }

        // 👉 FIX 3: TRẢ VỀ DANH SÁCH LỊCH SỬ CHO CƯ DÂN (BUG 59 VÀ 58)
        public async Task<ApiResponse<object>> GetMyContractAsync(int accountId)
        {
            var contracts = await _context.Contracts
                .Include(c => c.Apartment)
                .Where(c => c.AccountId == accountId && c.IsDeleted == false)
                .OrderByDescending(c => c.CreatedAt)
                .ToListAsync();

            if (!contracts.Any()) return ApiResponse<object>.Fail(404, "Bạn chưa có hợp đồng nào.");

            var resultList = new List<object>();

            foreach (var contract in contracts)
            {
                decimal? refundAmount = null;
                if (contract.Status == GeneralStatus.Inactive)
                {
                    var invoices = await _context.Invoices.Where(x => x.ContractId == contract.ContractId).ToListAsync();
                    var invoiceIds = invoices.Select(x => x.InvoiceId).ToList();
                    var payments = await _context.PaymentTransactions.Where(x => x.InvoiceId != null && invoiceIds.Contains(x.InvoiceId.Value)).ToListAsync();

                    decimal totalInvoice = invoices.Sum(x => x.TotalMoney ?? 0);
                    decimal totalPaid = payments.Sum(x => x.AmountPaid ?? 0);
                    decimal additionalCost = contract.AdditionalCost ?? 0;

                    refundAmount = totalPaid - totalInvoice - additionalCost;
                }

                resultList.Add(new
                {
                    ContractId = contract.ContractId,
                    ContractCode = contract.ContractCode,
                    ApartmentId = contract.ApartmentId,
                    ApartmentCode = contract.Apartment?.ApartmentCode ?? "N/A",
                    StartDay = contract.StartDay,
                    EndDay = contract.EndDay,
                    MonthlyRent = contract.MonthlyRent,
                    Deposit = contract.Deposit,
                    Status = contract.Status,
                    FileUrl = contract.File,
                    AdditionalCost = contract.AdditionalCost,
                    TerminationReason = contract.TerminationReason,
                    RefundAmount = refundAmount
                });
            }

            return ApiResponse<object>.Success(resultList, "OK");
        }

        // ==============================================================
        // HÀM XỬ LÝ CHỨC NĂNG THÙNG RÁC (SOFT DELETE, HARD DELETE, RESTORE)
        // ==============================================================

        public async Task<ApiResponse<object>> GetDeletedContractsAsync()
        {
            var deletedContracts = await _context.Contracts
                .Include(c => c.Account).ThenInclude(a => a.Info)
                .Include(c => c.Apartment)
                .Where(c => c.IsDeleted == true)
                .Select(c => new
                {
                    ContractId = c.ContractId,
                    ContractCode = c.ContractCode,
                    UpdatedAt = c.UpdatedAt,
                    Account = new
                    {
                        Info = new
                        {
                            FullName = c.Account != null && c.Account.Info != null ? c.Account.Info.FullName : "N/A",
                            PhoneNumber = c.Account != null && c.Account.Info != null ? c.Account.Info.PhoneNumber : "N/A"
                        },
                        Email = c.Account != null ? c.Account.Email : "N/A"
                    },
                    Apartment = new
                    {
                        ApartmentCode = c.Apartment != null ? c.Apartment.ApartmentCode : "N/A"
                    }
                })
                .ToListAsync();

            return ApiResponse<object>.Success(deletedContracts, "OK");
        }

        public async Task<ApiResponse<object>> SoftDeleteContractAsync(int contractId)
        {
            try
            {
                var contract = await _context.Contracts.FirstOrDefaultAsync(c => c.ContractId == contractId && c.IsDeleted != true);

                if (contract == null)
                    return ApiResponse<object>.Fail(404, $"Không tìm thấy hợp đồng mang ID {contractId} hoặc hợp đồng đã bị xóa trước đó.");

                contract.IsDeleted = true;
                contract.UpdatedAt = DateTime.Now;

                await _context.SaveChangesAsync();

                return ApiResponse<object>.Success(null, "Đã chuyển hợp đồng vào thùng rác thành công.");
            }
            catch (Exception ex)
            {
                return ApiResponse<object>.Fail(500, "Lỗi Server khi xóa mềm: " + ex.Message);
            }
        }

        public async Task<ApiResponse<object>> RestoreContractAsync(int contractId)
        {
            try
            {
                var contract = await _context.Contracts.FirstOrDefaultAsync(c => c.ContractId == contractId && c.IsDeleted == true);

                if (contract == null)
                    return ApiResponse<object>.Fail(404, "Không tìm thấy hợp đồng này trong thùng rác.");

                contract.IsDeleted = false;
                contract.UpdatedAt = DateTime.Now;

                await _context.SaveChangesAsync();

                return ApiResponse<object>.Success(null, "Đã khôi phục hợp đồng thành công.");
            }
            catch (Exception ex)
            {
                return ApiResponse<object>.Fail(500, "Lỗi Server khi khôi phục: " + ex.Message);
            }
        }

        public async Task<ApiResponse<object>> HardDeleteContractAsync(int contractId)
        {
            try
            {
                var contract = await _context.Contracts.FirstOrDefaultAsync(c => c.ContractId == contractId && c.IsDeleted == true);

                if (contract == null)
                    return ApiResponse<object>.Fail(404, "Không tìm thấy hợp đồng này trong thùng rác.");

                var versions = await _context.ContractVersions.Where(v => v.ContractId == contractId).ToListAsync();
                if (versions.Any())
                {
                    _context.ContractVersions.RemoveRange(versions);
                }

                _context.Contracts.Remove(contract);
                await _context.SaveChangesAsync();

                return ApiResponse<object>.Success(null, "Đã xóa vĩnh viễn hợp đồng khỏi hệ thống.");
            }
            catch (Exception ex)
            {
                return ApiResponse<object>.Fail(500, "Lỗi Server khi xóa vĩnh viễn: " + ex.Message);
            }
        }

        // US21 - View Deposit Settlement
        public async Task<DepositSettlementDto?> GetDepositSettlementAsync(int contractId, int? requestingAccountId = null)
        {
            var contract = await _context.Contracts
                .Include(c => c.Account)
                    .ThenInclude(a => a.Info)
                .Include(c => c.Apartment)
                .Where(c => c.ContractId == contractId && c.IsDeleted == false)
                .FirstOrDefaultAsync();

            if (contract == null) return null;

            // Nếu có truyền accountId (Resident gọi), kiểm tra quyền truy cập
            if (requestingAccountId.HasValue && contract.AccountId != requestingAccountId.Value)
                return null; // Không có quyền xem hợp đồng người khác

            return new DepositSettlementDto
            {
                ContractId = contract.ContractId,
                ContractCode = contract.ContractCode,
                ApartmentId = contract.ApartmentId,
                ApartmentCode = contract.Apartment?.ApartmentCode,
                ApartmentName = contract.Apartment?.ApartmentName,
                ResidentAccountId = contract.AccountId,
                ResidentName = contract.Account?.Info?.FullName,
                ResidentEmail = contract.Account?.Email,
                Deposit = contract.Deposit,
                AdditionalCost = contract.AdditionalCost,
                RefundAmount = contract.RefundAmount,
                StartDay = contract.StartDay,
                EndDay = contract.EndDay,
                MonthlyRent = contract.MonthlyRent,
                Status = contract.Status?.ToString(),
                TerminationReason = contract.TerminationReason,
                UpdatedAt = contract.UpdatedAt
            };
        }
    }
}