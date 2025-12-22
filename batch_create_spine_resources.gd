@tool
extends EditorScript

# 修改为你存放咖波资源的文件夹路径（相对于 res://）
const BASE_PATH = "res://assets/spine/"

func _run():
	var dir = DirAccess.open(BASE_PATH)
	if not dir:
		print("找不到目录: ", BASE_PATH)
		return

	_process_dir(BASE_PATH)
	print("--- 所有资源处理完成！ ---")

func _process_dir(path: String):
	var dir = DirAccess.open(path)
	dir.list_dir_begin()
	
	var file_name = dir.get_next()
	while file_name != "":
		if dir.current_is_dir():
			if file_name != "." and file_name != "..":
				_process_dir(path + file_name + "/")
		else:
			# 只处理骨骼文件
			if file_name.ends_with(".spine-json"):
				create_spine_data_resource(path, file_name)
		
		file_name = dir.get_next()

func create_spine_data_resource(folder_path: String, json_name: String):
	var base_name = json_name.replace(".spine-json", "")
	var atlas_path = folder_path + base_name + ".atlas"
	var json_path = folder_path + json_name
	var output_path = folder_path + base_name + ".res" # 生成的资源后缀

	# 检查图集是否存在
	if not FileAccess.file_exists(atlas_path):
		print("跳过: ", base_name, " (找不到对应的 .atlas)")
		return

	# 1. 加载骨骼和图集资源 (Godot 导入后的资源对象)
	var skeleton_file = load(json_path)
	var atlas_file = load(atlas_path)
	
	# 2. 创建 SpineSkeletonDataResource
	var data_res = SpineSkeletonDataResource.new()
	data_res.skeleton_file_res = skeleton_file
	data_res.atlas_res = atlas_file
	
	# 3. 保存到硬盘
	var error = ResourceSaver.save(data_res, output_path)
	if error == OK:
		print("成功创建: ", output_path)
	else:
		print("保存失败: ", base_name, " 错误代码: ", error)
