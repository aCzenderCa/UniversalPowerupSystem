# What is universal powerup system?
powerup is the content of the game 20MTD, each time after collecting
experience upgrade will extract powerup for players to choose. 
This system provides a simple and unified interface for designing 
custom upgrades.

If you don't need the custom refresh weights, custom refresh conditions and custom reset states
provided by this library, you can choose to call RegCustomPowerup(Powerup powerup) and you will
get the same as adding powerup to PowerupGenerator.Instance.powerupPool If you want to use the 
special functions provided by this library, you can call RegCustomPowerup(CustomPowerup customPowerup)
and modify the customPowerup.


# 什么是通用升级系统
powerup是游戏黎明前20分钟的内容，每次收集经验到升级时，会刷出多个powerup供玩家选取。
本系统为自定义powerup提供了简单统一的接口。

如果你不需要本库提供的自定义刷新权重、自定义刷新条件和自定义重置状态等功能，
可以选择调用RegCustomPowerup(Powerup powerup)，你会获得与将powerup加入PowerupGenerator
.Instance.powerupPool相同的效果，如果想要使用本库提供的特殊功能，
则可以调用RegCustomPowerup(CustomPowerup customPowerup)并修改customPowerup。
