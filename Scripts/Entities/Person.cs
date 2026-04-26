namespace Jogomania.Entities
{
    public enum Gender
    {
        Male,
        Female
    }

    public class Person
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public int Age { get; set; }
        public Gender Gender { get; set; }
        
        // Atributos Medianamente balanceados para a Caravana Inicial (Escala 0 a 100)
        public int Strength { get; set; } = 50;
        public int Intelligence { get; set; } = 50;
        public int Health { get; set; } = 100;

        public Person(int id, string name, Gender gender, int age)
        {
            Id = id;
            Name = name;
            Gender = gender;
            Age = age;
        }
    }
}
