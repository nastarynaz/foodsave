using System;

namespace FoodSave.Models
{
    public class RecipeHistory
    {
        // =========================
        // Attributes
        // =========================

        public int RecipeID { get; set; }
        public int UserID { get; set; }
        public string NamaResep { get; set; }
        public DateTime TanggalAkses { get; set; }

        // =========================
        // Methods
        // =========================

        public bool SimpanResep()
        {
            // TODO:
            // Save recipe access to database
            return true;
        }

        public RecipeHistory GetResep()
        {
            // TODO:
            // Retrieve recipe history
            return this;
        }
    }
}