using System.Collections.Generic;
using System.Threading.Tasks;
using APBD.Models;
namespace APBD.Services
{
    public interface IClientsService
    {
        Task<bool> ClientExistsAsync(int idClient);
        Task<IEnumerable<ClientTripDetails>> GetClientTripsAsync(int idClient);
        Task<(Client? client, string? errorMessage)> CreateClientAsync(Client client);
        Task<(bool success, string? errorMessage, bool clientNotFound, bool tripNotFound, bool tripDateInvalid, bool alreadyRegistered, bool tripFull)> AssignClientToTripAsync(int idClient, int idTrip);
        Task<bool> DeleteClientRegistrationAsync(int idClient, int idTrip);
        Task<bool> ClientHasTripsAsync(int idClient);
        Task<bool> DeleteClientAsync(int idClient);
    }
}
