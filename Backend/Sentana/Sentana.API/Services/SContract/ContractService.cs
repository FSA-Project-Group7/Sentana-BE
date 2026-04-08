using Microsoft.EntityFrameworkCore;
using Sentana.API.DTOs.Contracts;
using Sentana.API.Enums;
using Sentana.API.Helpers;
using Sentana.API.Models;
using Sentana.API.Repositories;
using Sentana.API.Services.SStorage;
using Sentana.API.Services.SEmail;
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
        private readonly IEmailService _emailService;

        public ContractService(
            IContractRepository contractRepository,
            IMinioService minioService,
            SentanaContext context,
            IEmailService emailService)
        {
            _contractRepository = contractRepository;
            _minioService = minioService;
            _context = context;
            _emailService = emailService;
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
            
            // Validate additionalCost
            if (request.AdditionalCost < 0) 
                return ApiResponse<object>.Fail(400, "Phí phát sinh không được là số âm");

            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var invoices = await _context.Invoices
                    .Where(x => x.ContractId == contractId)
                    .ToListAsync();

                var invoiceIds = invoices.Select(x => x.InvoiceId).ToList();

                var payments = await _context.PaymentTransactions
                    .Where(x => x.InvoiceId != null && invoiceIds.Contains(x.InvoiceId.Value))
                    .ToListAsync();

                decimal totalInvoice = invoices.Sum(x => x.TotalMoney ?? 0);
                decimal totalPaid = payments.Sum(x => x.AmountPaid ?? 0);
                decimal additionalCost = request.AdditionalCost;

                decimal refund = totalPaid - totalInvoice - additionalCost;
                bool isFullyPaid = totalPaid >= (totalInvoice + additionalCost);

                contract.Status = GeneralStatus.Inactive;
                contract.EndDay = request.TerminationDate;
                contract.UpdatedAt = DateTime.Now;

                contract.AdditionalCost = additionalCost;
                contract.RefundAmount = refund;
                contract.TerminationReason = request.TerminationReason;

                if (additionalCost > 0)
                {
                    contract.SettlementStatus = SettlementStatus.PendingSettlement;
                }
                else
                {
                    contract.SettlementStatus = SettlementStatus.Settled;
                    contract.SettledAt = DateTime.Now;
                }

                if (contract.Apartment != null && contract.Apartment.Status == ApartmentStatus.Occupied)
                {
                    contract.Apartment.Status = ApartmentStatus.Vacant;
                }

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
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                string paymentStatus = isFullyPaid ? "Đã thanh toán đủ" : "Chưa thanh toán đủ";

                // Gửi email thông báo cho resident
                await SendTerminationEmailAsync(contract, totalInvoice, totalPaid, additionalCost, refund);

                return ApiResponse<object>.Success(new
                {
                    TotalInvoice = totalInvoice,
                    TotalPaid = totalPaid,
                    AdditionalCost = additionalCost,
                    RefundAmount = refund,
                    IsFullyPaid = isFullyPaid,
                    PaymentStatus = paymentStatus,
                    SettlementStatus = contract.SettlementStatus.ToString() 
                }, "Chấm dứt hợp đồng thành công và đã lưu trữ sao kê tài chính.");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return ApiResponse<object>.Fail(500, ex.Message);
            }
        }

        private async Task SendTerminationEmailAsync(Contract contract, decimal totalInvoice, decimal totalPaid, decimal additionalCost, decimal refund)
        {
            try
            {
                if (contract.Account?.Email == null) return;

                var residentName = contract.Account.Info?.FullName ?? "Quý khách";
                var apartmentName = contract.Apartment?.ApartmentName ?? contract.Apartment?.ApartmentCode ?? "N/A";

                string refundStatus;
                string refundColor;
                if (refund > 0)
                {
                    refundStatus = $"<strong style='color: #28a745;'>BQL sẽ hoàn trả: {refund:N0} VNĐ</strong>";
                    refundColor = "#28a745";
                }
                else if (refund < 0)
                {
                    refundStatus = $"<strong style='color: #dc3545;'>Quý khách còn nợ: {Math.Abs(refund):N0} VNĐ</strong>";
                    refundColor = "#dc3545";
                }
                else
                {
                    refundStatus = "<strong style='color: #6c757d;'>Đã thanh toán đủ, không còn nợ</strong>";
                    refundColor = "#6c757d";
                }

                string emailBody = $@"
                    <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 20px; background-color: #f8f9fa;'>
                        <div style='background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); padding: 30px; border-radius: 10px 10px 0 0; text-align: center;'>
                            <h2 style='color: white; margin: 0;'>THÔNG BÁO THANH LÝ HỢP ĐỒNG</h2>
                        </div>
                        
                        <div style='background-color: white; padding: 30px; border-radius: 0 0 10px 10px; box-shadow: 0 2px 10px rgba(0,0,0,0.1);'>
                            <p style='font-size: 16px; color: #333;'>Kính gửi <strong>{residentName}</strong>,</p>
                            
                            <p style='color: #666; line-height: 1.6;'>
                                Hợp đồng thuê căn hộ <strong>{apartmentName}</strong> (Mã: <strong>{contract.ContractCode}</strong>) 
                                đã được thanh lý vào ngày <strong>{contract.EndDay?.ToString("dd/MM/yyyy")}</strong>.
                            </p>

                            <div style='background-color: #f8f9fa; padding: 20px; border-radius: 8px; margin: 20px 0;'>
                                <h3 style='color: #667eea; margin-top: 0; border-bottom: 2px solid #667eea; padding-bottom: 10px;'>
                                    📊 Bảng Đối Soát Tài Chính
                                </h3>
                                
                                <table style='width: 100%; border-collapse: collapse;'>
                                    <tr style='border-bottom: 1px solid #dee2e6;'>
                                        <td style='padding: 12px 0; color: #666;'>Tổng hóa đơn:</td>
                                        <td style='padding: 12px 0; text-align: right; font-weight: bold;'>{totalInvoice:N0} VNĐ</td>
                                    </tr>
                                    <tr style='border-bottom: 1px solid #dee2e6;'>
                                        <td style='padding: 12px 0; color: #666;'>Đã thanh toán:</td>
                                        <td style='padding: 12px 0; text-align: right; font-weight: bold; color: #28a745;'>{totalPaid:N0} VNĐ</td>
                                    </tr>
                                    <tr style='border-bottom: 1px solid #dee2e6;'>
                                        <td style='padding: 12px 0; color: #666;'>Phí phát sinh/Phạt:</td>
                                        <td style='padding: 12px 0; text-align: right; font-weight: bold; color: #dc3545;'>{additionalCost:N0} VNĐ</td>
                                    </tr>
                                    <tr style='background-color: #e9ecef;'>
                                        <td style='padding: 15px 10px; font-weight: bold; font-size: 16px;'>KẾT QUẢ:</td>
                                        <td style='padding: 15px 10px; text-align: right; font-size: 18px; color: {refundColor}; font-weight: bold;'>
                                            {(refund >= 0 ? "+" : "")}{refund:N0} VNĐ
                                        </td>
                                    </tr>
                                </table>

                                <div style='margin-top: 20px; padding: 15px; background-color: white; border-left: 4px solid {refundColor}; border-radius: 4px;'>
                                    {refundStatus}
                                </div>

                                {(string.IsNullOrEmpty(contract.TerminationReason) ? "" : $@"
                                <div style='margin-top: 15px; padding: 15px; background-color: #fff3cd; border-left: 4px solid #ffc107; border-radius: 4px;'>
                                    <strong>Lý do thanh lý:</strong><br/>
                                    <span style='color: #856404;'>{contract.TerminationReason}</span>
                                </div>
                                ")}
                            </div>

                            <div style='background-color: #e7f3ff; padding: 15px; border-radius: 8px; margin: 20px 0;'>
                                <p style='margin: 0; color: #004085;'>
                                    <strong>📌 Lưu ý:</strong> Công thức tính = Đã thanh toán - Tổng hóa đơn - Phí phát sinh
                                </p>
                            </div>

                            <p style='color: #666; line-height: 1.6;'>
                                Nếu có bất kỳ thắc mắc nào, vui lòng liên hệ Ban Quản Lý để được hỗ trợ.
                            </p>

                            <div style='margin-top: 30px; padding-top: 20px; border-top: 1px solid #dee2e6; text-align: center; color: #999;'>
                                <p style='margin: 5px 0;'>Trân trọng,</p>
                                <p style='margin: 5px 0; font-weight: bold; color: #667eea;'>Ban Quản Lý Sentana</p>
                            </div>
                        </div>
                    </div>
                ";

                await _emailService.SendEmailAsync(
                    contract.Account.Email,
                    $"[SENTANA] Thông báo thanh lý hợp đồng {contract.ContractCode}",
                    emailBody
                );
            }
            catch (Exception ex)
            {
                // Log error nhưng không throw để không ảnh hưởng đến terminate process
                Console.WriteLine($"Error sending termination email: {ex.Message}");
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

        public async Task<ApiResponse<object>> GetMyContractAsync(int accountId)
        {
            var contracts = await _context.Contracts
                .Include(c => c.Apartment)
                .Where(c => c.AccountId == accountId && c.IsDeleted == false)
                .OrderByDescending(c => c.CreatedAt)
                .ToListAsync();

            if (!contracts.Any()) return ApiResponse<object>.Fail(404, "Bạn chưa có hợp đồng nào.");

            var resultList = new List<object>();

            var relationships = await _context.Relationships.ToListAsync();

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

                var servicesList = new List<object>();
                var roommatesList = new List<object>();

                if (contract.ApartmentId.HasValue)
                {
                    int aptId = contract.ApartmentId.Value;
                    var aptServices = await _context.ApartmentServices
                        .Include(s => s.Service)
                        .Where(s => s.ApartmentId == aptId && s.IsDeleted == false)
                        .ToListAsync();

                    servicesList = aptServices.Select(s => new {
                        ServiceName = s.Service?.ServiceName,
                        UnitPrice = s.Service?.ServiceFee, 
                        ActualPrice = s.ActualPrice,
                        Unit = "tháng"                   
                    }).Cast<object>().ToList();

                    var aptResidents = await _context.ApartmentResidents
                        .Include(r => r.Account).ThenInclude(a => a.Info)
                        .Where(r => r.ApartmentId == aptId && r.AccountId != accountId && r.IsDeleted == false)
                        .ToListAsync();

                    roommatesList = aptResidents.Select(r => new {
                        FullName = r.Account?.Info?.FullName ?? r.Account?.UserName,
                        Phone = r.Account?.Info?.PhoneNumber,
                        Relationship = relationships.FirstOrDefault(rel => rel.RelationshipId == r.RelationshipId)?.RelationshipName ?? "Khác"
                    }).Cast<object>().ToList();
                }

                resultList.Add(new
                {
                    // Các trường để thg resident xem
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
                    RefundAmount = refundAmount,
                    SettlementStatus = contract.SettlementStatus,
                    SettledAt = contract.SettledAt,
                    Services = servicesList,
                    Roommates = roommatesList
                });
            }

            return ApiResponse<object>.Success(resultList, "OK");
        }
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

        // Settle Contract - Complete settlement process
        public async Task<ApiResponse<object>> SettleContractAsync(int contractId, SettleContractDto request)
        {
            var contract = await _context.Contracts
                .Include(c => c.Apartment)
                .FirstOrDefaultAsync(c => c.ContractId == contractId && c.IsDeleted == false);

            if (contract == null)
                return ApiResponse<object>.Fail(404, "Không tìm thấy hợp đồng");

            if (contract.Status != GeneralStatus.Inactive)
                return ApiResponse<object>.Fail(400, "Chỉ có thể quyết toán hợp đồng đã chấm dứt");

            if (contract.SettlementStatus == SettlementStatus.Settled)
                return ApiResponse<object>.Fail(400, "Hợp đồng đã được quyết toán rồi");

            if (contract.SettlementStatus != SettlementStatus.PendingInspection && 
                contract.SettlementStatus != SettlementStatus.PendingSettlement)
                return ApiResponse<object>.Fail(400, "Trạng thái hợp đồng không hợp lệ để quyết toán");

            // Validate additionalCost
            if (request.AdditionalCost < 0)
                return ApiResponse<object>.Fail(400, "Phí phát sinh không được là số âm");

            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // Cập nhật chi phí phát sinh nếu có
                if (request.AdditionalCost > 0)
                {
                    contract.AdditionalCost = request.AdditionalCost;
                }

                // Tính toán lại refund amount
                var invoices = await _context.Invoices
                    .Where(x => x.ContractId == contractId)
                    .ToListAsync();

                var invoiceIds = invoices.Select(x => x.InvoiceId).ToList();

                var payments = await _context.PaymentTransactions
                    .Where(x => x.InvoiceId != null && invoiceIds.Contains(x.InvoiceId.Value))
                    .ToListAsync();

                decimal totalInvoice = invoices.Sum(x => x.TotalMoney ?? 0);
                decimal totalPaid = payments.Sum(x => x.AmountPaid ?? 0);
                decimal additionalCost = contract.AdditionalCost ?? 0;

                contract.RefundAmount = totalPaid - totalInvoice - additionalCost;

                // Đánh dấu đã hoàn tất quyết toán
                contract.SettlementStatus = SettlementStatus.Settled;
                contract.SettledAt = DateTime.Now;
                contract.UpdatedAt = DateTime.Now;

                // Lưu note nếu cần (có thể thêm field SettlementNote vào model Contract)
                if (!string.IsNullOrEmpty(request.Note))
                {
                    contract.TerminationReason = contract.TerminationReason + 
                        (string.IsNullOrEmpty(contract.TerminationReason) ? "" : " | ") + 
                        $"Ghi chú quyết toán: {request.Note}";
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return ApiResponse<object>.Success(new
                {
                    ContractId = contract.ContractId,
                    RefundAmount = contract.RefundAmount,
                    SettlementStatus = contract.SettlementStatus.ToString(),
                    SettledAt = contract.SettledAt
                }, "Quyết toán hợp đồng thành công");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return ApiResponse<object>.Fail(500, "Lỗi server khi quyết toán: " + ex.Message);
            }
        }
    }
}