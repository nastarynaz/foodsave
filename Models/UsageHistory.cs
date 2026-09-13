using System;
using System.Collections.Generic;

namespace FoodSave.Models
{
    public class UsageHistory
    {
        public int HistoryID { get; set; }
        public int FoodID { get; set; }
        public DateTime TanggalPenggunaan { get; set; }
        public int JumlahTerpakai { get; set; }
        public string StatusPenggunaan { get; set; }

        public bool SimpanRiwayat()
        {
            // TODO: Save usage history to database
            return true;
        }

        public List<UsageHistory> GetRiwayat()
        {
            // TODO: Retrieve usage history
            return new List<UsageHistory>();
        }
    }
}