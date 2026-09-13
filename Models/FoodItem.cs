using System;
using System.Collections.Generic;

namespace FoodSave.Models
{
    public class FoodItem
    {
        // =========================
        // Attributes
        // =========================

        public int FoodID { get; set; }
        public int UserID { get; set; }
        public string NamaMakanan { get; set; }
        public string Kategori { get; set; }
        public int Jumlah { get; set; }
        public DateTime TanggalPembelian { get; set; }
        public DateTime TanggalKedaluwarsa { get; set; }

        // =========================
        // Relationships
        // =========================

        public List<UsageHistory> UsageHistories { get; set; }

        // =========================
        // Constructor
        // =========================

        public FoodItem()
        {
            UsageHistories = new List<UsageHistory>();
        }

        // =========================
        // Methods
        // =========================

        public bool TambahStok()
        {
            // TODO:
            // Implement add stock logic
            return true;
        }

        public bool UpdateStok()
        {
            // TODO:
            // Implement update stock logic
            return true;
        }

        public bool HapusStok()
        {
            // TODO:
            // Implement delete stock logic
            return true;
        }

        public FoodItem GetDetailItem()
        {
            return this;
        }
    }
}