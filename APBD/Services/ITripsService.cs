
using System.Collections.Generic;
using System.Threading.Tasks;
using APBD.Models;

namespace APBD.Services
{
    public interface ITripsService
    {
        Task<IEnumerable<Trip>> GetTripsAsync();
    }
}

