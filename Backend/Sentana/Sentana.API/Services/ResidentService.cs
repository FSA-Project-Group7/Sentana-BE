using Microsoft.EntityFrameworkCore;
using Sentana.API.DTOs.Resident;
using Sentana.API.Enums;
using Sentana.API.Models;

public class ResidentService
{
    private readonly SentanaContext _context;

    public ResidentService(SentanaContext context)
    {
        _context = context;
    }

    public async Task<bool> AssignResident(AssignResidentRequestDto request)
    {
        var resident = await _context.ApartmentResidents
            .FirstOrDefaultAsync(r => r.ResidentId == request.ResidentId && r.IsDeleted == false);

        if (resident == null)
            return false;

        resident.ApartmentId = request.ApartmentId;
        resident.Status = GeneralStatus.Active;

        await _context.SaveChangesAsync();

        return true;
    }
}