using System;

namespace BeatHero.Data
{
    [Serializable]
    public class BeatUnit
    {
        public NoteLength noteLength;
        public GridEffectShape gridEffectShape; // null = 쉼표
    }
}
