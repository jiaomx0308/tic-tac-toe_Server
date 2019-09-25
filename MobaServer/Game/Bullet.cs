using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MyLib
{
    public class Bullet
    {
        public static int ID = 0;
        public int id = 0;

        public Room room;
        public int playerID;

        //位置与运动方向
        public int px;
        public int py;
        public int dir;

        public int speed = 1;
        public int lifeTime = 0;
        public bool isDead = false;

        public Bullet(Room room, int PlayerId, int currentPx, int currentPy, int moveDir)
        {
            this.id = ++ID;
            this.room = room;
            this.playerID = PlayerId;
            this.dir = moveDir;
            this.px = currentPx;
            this.py = currentPy;
        }

        public void Update()
        {
            if (isDead)
                return;

            switch (dir)
            {
                case 0:
                    px += 1;
                    break;
                case 1:
                    py += 1;
                    break;
                case 2:
                    px -= 1;
                    break;
                case 3:
                    py -= 1;
                    break;
                default:
                    break;
            }

            room.CheckDamage(this);


            lifeTime++;

            if (lifeTime > 4)
                isDead = true;
        }
    }
}
