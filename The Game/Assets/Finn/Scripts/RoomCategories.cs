using System;

[Flags]
public enum RoomCategory
{
    None = 0,
    // Mansion
    Bedroom = 1 << 0,
    Bathroom = 1 << 1,
    Kitchen = 1 << 2,
    Dining = 1 << 3,
    Library = 1 << 4,
    Lounge = 1 << 5,
    Study = 1 << 6,
    // Bunker
    Armory = 1 << 7,
    Barracks = 1 << 8,
    Infirmary = 1 << 9,
    ControlRoom = 1 << 10,
    MessHall = 1 << 11,
    Storage = 1 << 12,
    Workshop = 1 << 13,
    // Office
    Cubicles = 1 << 14,
    Conference = 1 << 15,
    BreakRoom = 1 << 16,
    ServerRoom = 1 << 17,
    Reception = 1 << 18,
    CopyRoom = 1 << 19,
}