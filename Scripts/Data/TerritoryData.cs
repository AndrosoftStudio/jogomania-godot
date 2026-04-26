using Godot;
using System.Collections.Generic;

namespace Jogomania.Data
{
    public class Village
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public int Population { get; set; }
    }

    public class City
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public List<Village> Villages { get; set; } = new List<Village>();
    }

    public class State
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public List<City> Cities { get; set; } = new List<City>();
    }

    public class Country
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public List<State> States { get; set; } = new List<State>();
        public Color CountryColor { get; set; }
    }

    public class Continent
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public List<Country> Countries { get; set; } = new List<Country>();
    }
}
