CREATE TABLE IF NOT EXISTS users (
  user_id integer GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
  nama varchar(120) NOT NULL,
  email varchar(254) NOT NULL UNIQUE,
  password_hash text NOT NULL
);

CREATE TABLE IF NOT EXISTS food_items (
  food_id integer GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
  user_id integer NOT NULL REFERENCES users(user_id) ON DELETE CASCADE,
  nama_makanan varchar(150) NOT NULL,
  kategori varchar(80) NOT NULL,
  jumlah integer NOT NULL CHECK (jumlah >= 0),
  tanggal_pembelian date NOT NULL,
  tanggal_kedaluwarsa date NOT NULL,
  is_archived boolean NOT NULL DEFAULT false,
  CONSTRAINT food_dates_valid CHECK (tanggal_kedaluwarsa >= tanggal_pembelian)
);
CREATE INDEX IF NOT EXISTS food_items_owner_expiry ON food_items(user_id, is_archived, tanggal_kedaluwarsa);

CREATE TABLE IF NOT EXISTS usage_history (
  history_id integer GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
  food_id integer NOT NULL REFERENCES food_items(food_id) ON DELETE RESTRICT,
  tanggal_penggunaan timestamptz NOT NULL DEFAULT now(),
  jumlah_terpakai integer NOT NULL CHECK (jumlah_terpakai > 0),
  status_penggunaan varchar(12) NOT NULL CHECK (status_penggunaan IN ('Digunakan', 'Dibuang'))
);
CREATE INDEX IF NOT EXISTS usage_history_food_date ON usage_history(food_id, tanggal_penggunaan DESC);

CREATE TABLE IF NOT EXISTS recipe_history (
  recipe_id integer GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
  user_id integer NOT NULL REFERENCES users(user_id) ON DELETE CASCADE,
  meal_api_id varchar(30) NOT NULL,
  nama_resep varchar(200) NOT NULL,
  tanggal_akses timestamptz NOT NULL DEFAULT now()
);
CREATE INDEX IF NOT EXISTS recipe_history_owner_date ON recipe_history(user_id, tanggal_akses DESC);
