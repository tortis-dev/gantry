using Docker.DotNet.Models;
using System.Collections.Generic;

namespace Gantry.Containers;

class ContainerListItem : ObservableObject
{
    readonly string _id;
    string _image;
    string _command;
    string _status;
    string _ports;
    string _name;
    string _size;
    string _labels;
    string _state;

    public ContainerListItem(string id, string image, string command, string status, string ports, string name, string size, string labels, string state)
    {
        _id = id;
        _image = image;
        _command = command;
        _status = status;
        _ports = ports;
        _name = name;
        _size = size;
        _labels = labels;
        _state = state;
    }

    public string Id
    {
        get => _id;
        init
        {
            if (SetField(ref _id, value))
            {
                OnPropertyChanged(nameof(ShortId));
            }
        }
    }

    public string ShortId => Id.Substring(0, 12);

    public string Image
    {
        get => _image;
        set => SetField(ref _image, value);
    }

    public string Command
    {
        get => _command;
        set => SetField(ref _command, value);
    }

    public string Status
    {
        get => _status;
        set => SetField(ref _status, value);
    }

    public string Ports
    {
        get => _ports;
        set => SetField(ref _ports, value);
    }

    public string Name
    {
        get => _name;
        set => SetField(ref _name, value);
    }

    public string Size
    {
        get => _size;
        set => SetField(ref _size, value);
    }

    public string Labels
    {
        get => _labels;
        set => SetField(ref _labels, value);
    }

    public string State
    {
        get => _state;
        set
        {
            if (SetField(ref _state, value))
            {
                OnPropertyChanged(nameof(IsRunning));
                OnPropertyChanged(nameof(IsStopped));
            }
        }
    }

    public bool IsRunning => State == "running";
    public bool IsStopped => State == "exited";
}