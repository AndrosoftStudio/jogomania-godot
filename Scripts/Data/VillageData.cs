using Godot;

namespace Jogomania.Data
{
    public class VillageData
    {
        public int Id { get; set; }
        public string Name { get; set; }
        
        // Coodenadas X,Y puras no Mapa 2D
        public int X { get; set; }
        public int Y { get; set; }

        public int OwnerId { get; set; } // 0 = Neutro, 1 = Player, 2+ = IA

        // Opcional: Nível de evolução da aldeia
        public int Level { get; set; } = 1; // 1 = Tribo, 2 = Principado (Cidade), etc.
    }
}
