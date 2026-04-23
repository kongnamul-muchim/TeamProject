import json
import sys

def analyze():
    with open("Assets/Shaders/Test.shadergraph", "r", encoding="utf8") as f:
        data = json.load(f)
    
    nodes = {n["m_ObjectId"]: n for n in data["m_Nodes"]}
    
    # print all nodes
    for k, v in nodes.items():
        name = v.get("m_Name", v.get("m_Type"))
        print(f"Node {k}: {name}")
    
    # print all edges
    print("\nEdges:")
    for edge in data["m_Edges"]:
        out_node_id = edge["m_OutputSlot"]["m_Node"]["m_Id"]
        out_slot = edge["m_OutputSlot"]["m_SlotId"]
        in_node_id = edge["m_InputSlot"]["m_Node"]["m_Id"]
        in_slot = edge["m_InputSlot"]["m_SlotId"]
        
        out_name = nodes[out_node_id].get("m_Name", out_node_id) if out_node_id in nodes else out_node_id
        in_name = nodes[in_node_id].get("m_Name", in_node_id) if in_node_id in nodes else in_node_id
        print(f"{out_name} (out {out_slot}) -> {in_name} (in {in_slot})")

if __name__ == "__main__":
    analyze()
