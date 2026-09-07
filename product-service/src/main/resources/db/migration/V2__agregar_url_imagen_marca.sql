-- El scraper envia el enlace a la ficha en la tienda, la imagen y la
-- marca para que el frontend pueda mostrarlos sin inventar valores.
-- Son opcionales: productos cargados a mano pueden no traerlos.
ALTER TABLE productos ADD COLUMN url TEXT;
ALTER TABLE productos ADD COLUMN imagen TEXT;
ALTER TABLE productos ADD COLUMN marca TEXT;