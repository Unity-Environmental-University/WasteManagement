using UnityEngine;

namespace _project.Scripts.UI
{
    [CreateAssetMenu(fileName = "DynamicSprite", menuName = "WasteManagement/DynamicSprite", order = 1)]
    public class DynamicSpriteSO : ScriptableObject
    {
        [SerializeField] public Sprite spriteStage01;
        [SerializeField] public Sprite spriteStage02;
        [SerializeField] public Sprite spriteStage03;


        public Sprite GetSpriteForEra(int era)
        {
            return era switch
            {
                1 => spriteStage01,
                2 => spriteStage02,
                3 => spriteStage03,
                _ => null
            };
        }
    }
}
