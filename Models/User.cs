using System.Collections.Generic;

namespace piringku.Models
{
    public class User
    {
        // =========================
        // Attributes
        // =========================

        public int UserID { get; set; }
        public string Nama { get; set; }
        public string Email { get; set; }

        // IMPORTANT:
        // Store a password hash here in the actual application,
        // not the user's plaintext password.
        public string Password { get; set; }

        // =========================
        // Relationships
        // =========================

        public List<FoodItem> FoodItems { get; set; }
        public List<RecipeHistory> RecipeHistories { get; set; }

        // =========================
        // Constructor
        // =========================

        public User()
        {
            FoodItems = new List<FoodItem>();
            RecipeHistories = new List<RecipeHistory>();
        }

        // =========================
        // Methods
        // =========================

        public bool DaftarAkun()
        {
            // TODO:
            // Implement registration logic
            return true;
        }

        public bool Login()
        {
            // TODO:
            // Implement login logic
            return true;
        }

        public void Logout()
        {
            // TODO:
            // Implement logout logic
        }

        public User GetProfil()
        {
            return this;
        }
    }
}