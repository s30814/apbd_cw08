namespace APBD.Models
{
    public class ClientTripDetails : Trip
    {
        public int RegisteredAt { get; set; }
        public int? PaymentDate { get; set; }
    }
}
