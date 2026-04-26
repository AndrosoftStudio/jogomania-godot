using Godot;
using System.Collections.Generic;

namespace Jogomania.Entities
{
    public class Caravan
    {
        public int Id { get; set; }
        public int OwnerPlayerId { get; set; } // ID do jogador dono (para o Multiplayer futuramente)
        public Vector2I Position { get; set; } // Posição exata no mapa global (em blocos de unidade)
        public List<Person> Inhabitants { get; private set; }

        public Caravan(int id, int playerId, Vector2I startPosition)
        {
            Id = id;
            OwnerPlayerId = playerId;
            Position = startPosition;
            Inhabitants = new List<Person>();
            InitializeStartingPopulation();
        }

        private void InitializeStartingPopulation()
        {
            // Criando 10 Homens e 10 Mulheres de 25 anos
            int personCounter = 1;
            
            for (int i = 0; i < 10; i++)
            {
                Inhabitants.Add(new Person(personCounter++, $"Homem {i+1}", Gender.Male, 25));
            }
            
            for (int i = 0; i < 10; i++)
            {
                Inhabitants.Add(new Person(personCounter++, $"Mulher {i+1}", Gender.Female, 25));
            }
        }
    }
}
